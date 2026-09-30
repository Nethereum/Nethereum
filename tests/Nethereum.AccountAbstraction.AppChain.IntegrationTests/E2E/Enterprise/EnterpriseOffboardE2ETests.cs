using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Enterprise
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "E2E-AppChainModernization")]
    [Trait("UseCase", "EnterpriseOffboard")]
    public class EnterpriseOffboardE2ETests
    {
        private const int GuardianThreshold = 2;
        private const int GuardianCount = 3;
        private const int Cap = 100;
        private const int WithinCapValue = 60;

        private readonly AppChainModularBundlerFixture _fixture;

        public EnterpriseOffboardE2ETests(AppChainModularBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.OperatorWeb3)
                .UseDeploymentAddresses(_fixture.Deployment.ToAADeploymentAddresses())
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        [Fact]
        public async Task Given_an_enrolled_account_with_a_capped_session_and_guardians_When_the_org_offboards_it_in_two_phases_Then_each_step_severs_the_capability_it_targets()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 41 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 41;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());
            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountService.UseAccountAbstraction(account, client);

            var operatorKey = EthECKey.GenerateKey();
            var capConfigInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", _fixture.CapRuleId),
                new ABIValue("uint256", (BigInteger)Cap));
            var depositSelector = new DepositFunction().GetCallData();

            var sessionSalt = new byte[32];
            sessionSalt[31] = 42;

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(d.Modules.EcdsaSessionValidator)
                .WithSessionValidatorInitData(operatorKey.GetPublicAddress())
                .WithSalt(sessionSalt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(_fixture.PayableTarget.ContractAddress)
                    .WithSelector(depositSelector)
                    .WithPolicy(_fixture.ValueCapCombinator.ContractAddress, capConfigInitData)
                    .Build());

            var session = sessionConfig.ToSession();
            var smartSessionQuery = new SmartSessionService(_fixture.OperatorWeb3, d.Modules.SmartSession);
            var permissionId = await smartSessionQuery.GetPermissionIdQueryAsync(session);

            sessionConfig.ModuleAddress = d.Modules.SmartSession;
            var installSessionReceipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(installSessionReceipt.UserOpSuccess, installSessionReceipt.FailureDiagnostic);
            Assert.True(await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()));

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
                guardianKeys.Select(k => k.GetPublicAddress()));
            var installGuardiansReceipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                d.Modules.SocialRecovery, GuardianThreshold, sortedGuardians);
            Assert.True(installGuardiansReceipt.UserOpSuccess, installGuardiansReceipt.FailureDiagnostic);

            var ecdsaValidator = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            Assert.Equal(owner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());

            var operatorAccount = client.GetAccount(
                accountAddress,
                new SmartSessionKeySigningService(operatorKey, permissionId),
                new SmartSessionValidatorModule(d.Modules.SmartSession, permissionId));
            var payableTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            payableTarget.UseAccountAbstraction(operatorAccount, client);

            var targetBalanceBeforeBaseline = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            var baselineReceipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapValue });
            Assert.True(baselineReceipt.UserOpSuccess, baselineReceipt.FailureDiagnostic);
            var targetBalanceAfterBaseline = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            Assert.Equal(targetBalanceBeforeBaseline.Value + WithinCapValue, targetBalanceAfterBaseline.Value);


            var ownerBeforeUnderThreshold = await ecdsaValidator.GetOwnerQueryAsync(accountAddress);
            var oneGuardian = guardianKeys.Take(1).ToArray();
            var underThresholdAccount = client.GetAccount(
                accountAddress,
                new MultiGuardianSigningService(oneGuardian, 1),
                new SocialRecoveryValidatorModule(d.Modules.SocialRecovery, 1));
            var underThresholdEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            underThresholdEcdsa.UseAccountAbstraction(underThresholdAccount, client);

            var underThresholdEx = await Assert.ThrowsAnyAsync<Exception>(() =>
                underThresholdEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(EthECKey.GenerateKey().GetPublicAddress()));
            Assert.False(underThresholdEx is Xunit.Sdk.XunitException,
                $"Expected an on-chain rejection, but a test assertion threw instead: {underThresholdEx}");

            var underThresholdMessages = FlattenMessages(underThresholdEx);
            Assert.True(underThresholdMessages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection, but got: {underThresholdEx}");
            var invalidSignatureSelector = Sha3Keccack.Current
                .CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.True(underThresholdMessages.IndexOf(invalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the InvalidSignature() quorum length-guard revert ({invalidSignatureSelector}), but got: {underThresholdEx}");
            Assert.Equal(ownerBeforeUnderThreshold.ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());

            var newOwner = EthECKey.GenerateKey();
            var recoveryAccount = client.GetAccount(
                accountAddress,
                new MultiGuardianSigningService(guardianKeys, GuardianThreshold),
                new SocialRecoveryValidatorModule(d.Modules.SocialRecovery, GuardianThreshold));
            var recoveryEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            recoveryEcdsa.UseAccountAbstraction(recoveryAccount, client);
            var recoveryReceipt = (AATransactionReceipt)await recoveryEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(
                newOwner.GetPublicAddress());
            Assert.True(recoveryReceipt.UserOpSuccess, recoveryReceipt.FailureDiagnostic);
            Assert.Equal(newOwner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());

            var oldOwnerAccount = client.GetAccount(
                accountAddress,
                new AccountSigningOfflineService(owner),
                new EcdsaValidatorModule(d.Modules.EcdsaValidator));
            var oldOwnerEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            oldOwnerEcdsa.UseAccountAbstraction(oldOwnerAccount, client);

            var oldOwnerEx = await Assert.ThrowsAnyAsync<Exception>(() =>
                oldOwnerEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(EthECKey.GenerateKey().GetPublicAddress()));
            Assert.False(oldOwnerEx is Xunit.Sdk.XunitException,
                $"Expected the old owner's signature to be rejected on-chain, but a test assertion threw instead: {oldOwnerEx}");
            var oldOwnerMessages = FlattenMessages(oldOwnerEx);
            Assert.True(oldOwnerMessages.IndexOf("AA24", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA24 signature-error rejection (the old owner is no longer the signer), but got: {oldOwnerEx}");
            Assert.Equal(newOwner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());


            var newOwnerAccount = client.GetAccount(
                accountAddress,
                new AccountSigningOfflineService(newOwner),
                new EcdsaValidatorModule(d.Modules.EcdsaValidator));

            var smartSessionService = new SmartSessionService(_fixture.OperatorWeb3, d.Modules.SmartSession);
            smartSessionService.UseAccountAbstraction(newOwnerAccount, client);
            var removeSessionReceipt = (AATransactionReceipt)await smartSessionService.RemoveSessionRequestAndWaitForReceiptAsync(permissionId);
            Assert.True(removeSessionReceipt.UserOpSuccess, removeSessionReceipt.FailureDiagnostic);

            var revokedEx = await Assert.ThrowsAnyAsync<Exception>(() => payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapValue }));
            Assert.False(revokedEx is Xunit.Sdk.XunitException,
                $"Expected the revoked session key to be rejected on-chain, but a test assertion threw instead: {revokedEx}");
            var revokedMessages = FlattenMessages(revokedEx);
            Assert.True(revokedMessages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (session no longer enabled), but got: {revokedEx}");
            var invalidPermissionIdSelector = Sha3Keccack.Current
                .CalculateHash(Encoding.UTF8.GetBytes("InvalidPermissionId(bytes32)"))[..4].ToHex(true);
            Assert.True(revokedMessages.IndexOf(invalidPermissionIdSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the SmartSession InvalidPermissionId(bytes32) selector {invalidPermissionIdSelector} (session revoked), but got: {revokedEx}");
            var targetBalanceAfterRevoke = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            Assert.Equal(targetBalanceAfterBaseline.Value, targetBalanceAfterRevoke.Value);

            var accountServiceNewOwner = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountServiceNewOwner.UseAccountAbstraction(newOwnerAccount, client);
            var uninstallReceipt = (AATransactionReceipt)await accountServiceNewOwner.UninstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(uninstallReceipt.UserOpSuccess, uninstallReceipt.FailureDiagnostic);

            var accountQueryService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            Assert.False(await accountQueryService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()),
                "Expected the SmartSession module to be uninstalled from the account.");

            var treasury = EthECKey.GenerateKey().GetPublicAddress();
            var treasuryBalanceBeforeSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(treasury);
            Assert.Equal(BigInteger.Zero, treasuryBalanceBeforeSweep.Value);

            var gasReserve = Nethereum.Web3.Web3.Convert.ToWei(0.1m);
            var accountBalanceBeforeSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.True(accountBalanceBeforeSweep.Value > gasReserve,
                $"Expected the account to still hold more than the gas reserve before sweeping, but it held {accountBalanceBeforeSweep.Value}.");
            var sweepAmount = accountBalanceBeforeSweep.Value - gasReserve;

            var sweepReceipt = (AATransactionReceipt)await accountServiceNewOwner.ExecuteAsync(
                new Call { Target = treasury, Value = sweepAmount, Data = Array.Empty<byte>() });
            Assert.True(sweepReceipt.UserOpSuccess, sweepReceipt.FailureDiagnostic);

            var treasuryBalanceAfterSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(treasury);
            Assert.Equal(sweepAmount, treasuryBalanceAfterSweep.Value);
            var accountBalanceAfterSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.True(accountBalanceAfterSweep.Value < gasReserve,
                $"Expected the account balance to drop below the gas reserve after the sweep, but it was {accountBalanceAfterSweep.Value}.");

            await _fixture.AdminService.BanUserAsync(accountAddress, "offboarded");
            Assert.False(await _fixture.AdminService.IsActiveAsync(accountAddress),
                "Expected the account to no longer be Active after the registry ban.");
        }

        private static string FlattenMessages(Exception ex)
        {
            var builder = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }
    }
}
