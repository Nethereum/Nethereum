using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization;
using Nethereum.AccountAbstraction.AppChain.Services;
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
    [Trait("UseCase", "EnterpriseLifecycle")]
    public class EnterpriseLifecycleE2ETests
    {
        private const string UserId = "alice@enterprise.example";
        private const int GuardianThreshold = 2;
        private const int GuardianCount = 3;
        private const int Tier2Threshold = 2;

        private const int Tier1Cap = 100;
        private const int Tier2Cap = 1000;
        private const int Tier1SmallValue = 60;
        private const int Tier2LargeValue = 600;
        private const int Tier2OverCapValue = 1500;

        private readonly AppChainModularBundlerFixture _fixture;

        public EnterpriseLifecycleE2ETests(AppChainModularBundlerFixture fixture)
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
        public async Task Given_the_two_admin_and_user_command_sections_When_a_users_account_lives_through_enroll_role_and_guardian_installs_operation_and_a_two_phase_offboard_Then_every_capability_and_every_rejection_holds_on_chain()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();
            var depositSelector = new DepositFunction().GetCallData();

            var owner = EthECKey.GenerateKey();
            var admin = new EnterpriseDirectoryAdminService(
                _fixture.AdminService,
                new Dictionary<string, string> { [UserId] = owner.GetPublicAddress() });

            var accountAddress = await admin.EnrollAsync(UserId, salt: 91);
            Assert.True(await admin.IsActiveAsync(UserId));
            Assert.Equal(accountAddress, admin.ResolveAddress(UserId));
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 91;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());

            var userSection = new EnterpriseAccountOperatorService(_fixture.OperatorWeb3, client, d, account);
            Assert.Equal(accountAddress.ToLowerInvariant(), userSection.AccountAddress.ToLowerInvariant());

            var operatorKey = EthECKey.GenerateKey();
            var tier2Owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var sortedTier2Owners = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
                tier2Owners.Select(k => k.GetPublicAddress())).ToList();

            var tier1Salt = new byte[32]; tier1Salt[31] = 92;
            var tier2Salt = new byte[32]; tier2Salt[31] = 93;

            var tier1Spec = new CappedRoleSpec(
                sessionKeyAddress: operatorKey.GetPublicAddress(),
                targetAddress: _fixture.PayableTarget.ContractAddress,
                functionSelector: depositSelector,
                policyAddress: _fixture.ValueCapCombinator.ContractAddress,
                policyRuleId: _fixture.CapRuleId,
                cap: Tier1Cap,
                salt: tier1Salt);

            var tier2Spec = new QuorumRoleSpec(
                validatorAddress: _fixture.OwnableValidator.ContractAddress,
                ownerAddresses: sortedTier2Owners,
                threshold: Tier2Threshold,
                targetAddress: _fixture.PayableTarget.ContractAddress,
                functionSelector: depositSelector,
                policyAddress: _fixture.ValueCapCombinator.ContractAddress,
                policyRuleId: _fixture.CapRuleId,
                cap: Tier2Cap,
                salt: tier2Salt);

            var tieredResult = await userSection.InstallTieredRolesAsync(tier1Spec, tier2Spec);
            Assert.True(tieredResult.Receipt.UserOpSuccess, tieredResult.Receipt.FailureDiagnostic);

            var accountQuery = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            Assert.True(await accountQuery.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()));

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
                guardianKeys.Select(k => k.GetPublicAddress())).ToList();

            var guardianReceipt = (AATransactionReceipt)await userSection.InstallGuardiansAsync(
                d.Modules.SocialRecovery, GuardianThreshold, sortedGuardians);
            Assert.True(guardianReceipt.UserOpSuccess, guardianReceipt.FailureDiagnostic);

            var ecdsaValidator = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            Assert.Equal(owner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());

            var tier1Account = client.GetAccount(
                accountAddress,
                new SmartSessionKeySigningService(operatorKey, tieredResult.Tier1PermissionId),
                new SmartSessionValidatorModule(d.Modules.SmartSession, tieredResult.Tier1PermissionId));
            var tier1Target = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            tier1Target.UseAccountAbstraction(tier1Account, client);

            var balanceBeforeTier1 = await GetTargetBalanceAsync();
            var tier1Receipt = (AATransactionReceipt)await tier1Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier1SmallValue });
            Assert.True(tier1Receipt.UserOpSuccess, tier1Receipt.FailureDiagnostic);
            var balanceAfterTier1 = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeTier1.Value + Tier1SmallValue, balanceAfterTier1.Value);

            var quorumSigningService = new OwnableValidatorSessionSigningService(
                tieredResult.Tier2PermissionId, new[] { tier2Owners[0], tier2Owners[1] }, Tier2Threshold);
            var quorumValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tieredResult.Tier2PermissionId, signatureSlotCount: Tier2Threshold);
            var tier2Account = client.GetAccount(accountAddress, quorumSigningService, quorumValidatorModule);
            var tier2Target = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            tier2Target.UseAccountAbstraction(tier2Account, client);

            var balanceBeforeTier2 = await GetTargetBalanceAsync();
            var tier2Receipt = (AATransactionReceipt)await tier2Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier2LargeValue });
            Assert.True(tier2Receipt.UserOpSuccess, tier2Receipt.FailureDiagnostic);
            var balanceAfterTier2 = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeTier2.Value + Tier2LargeValue, balanceAfterTier2.Value);


            var underQuorumSigningService = new OwnableValidatorSessionSigningService(
                tieredResult.Tier2PermissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { tier2Owners[0] }, 1));
            var underQuorumValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tieredResult.Tier2PermissionId, signatureSlotCount: 1);
            var underQuorumAccount = client.GetAccount(accountAddress, underQuorumSigningService, underQuorumValidatorModule);
            var underQuorumTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            underQuorumTarget.UseAccountAbstraction(underQuorumAccount, client);

            var balanceBeforeUnderQuorum = await GetTargetBalanceAsync();
            var underQuorumEx = await Assert.ThrowsAnyAsync<Exception>(() => underQuorumTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier2LargeValue }));
            AssertOnChainRejection(underQuorumEx, "AA23", InvalidSignatureSelector, "the authority length guard (under-quorum)");
            Assert.Equal(balanceBeforeUnderQuorum.Value, (await GetTargetBalanceAsync()).Value);

            var overCapAccount = client.GetAccount(accountAddress, quorumSigningService, quorumValidatorModule);
            var overCapTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            overCapTarget.UseAccountAbstraction(overCapAccount, client);

            var balanceBeforeOverCap = await GetTargetBalanceAsync();
            var overCapEx = await Assert.ThrowsAnyAsync<Exception>(() => overCapTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier2OverCapValue }));
            AssertOnChainRejection(overCapEx, "AA23", PolicyViolationSelector, "the cap policy (tier-2 over-cap)");
            var overCapMessages = FlattenMessages(overCapEx);
            Assert.True(overCapMessages.IndexOf(InvalidSignatureSelector, StringComparison.OrdinalIgnoreCase) < 0,
                $"Expected NO authority InvalidSignature() marker - the cap policy, not the authority, must have rejected this op - but got: {overCapEx}");
            Assert.Equal(balanceBeforeOverCap.Value, (await GetTargetBalanceAsync()).Value);

            var balanceBeforeTierBoundary = await GetTargetBalanceAsync();
            var tierBoundaryEx = await Assert.ThrowsAnyAsync<Exception>(() => tier1Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier2LargeValue }));
            AssertOnChainRejection(tierBoundaryEx, "AA23", PolicyViolationSelector, "tier-1's own cap policy (tier boundary)");
            Assert.Equal(balanceBeforeTierBoundary.Value, (await GetTargetBalanceAsync()).Value);


            var ownerBeforeUnderThreshold = await ecdsaValidator.GetOwnerQueryAsync(accountAddress);
            var underThresholdAccount = client.GetAccount(
                accountAddress,
                new MultiGuardianSigningService(guardianKeys.Take(1).ToArray(), 1),
                new SocialRecoveryValidatorModule(d.Modules.SocialRecovery, 1));
            var underThresholdEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            underThresholdEcdsa.UseAccountAbstraction(underThresholdAccount, client);

            var underThresholdEx = await Assert.ThrowsAnyAsync<Exception>(() =>
                underThresholdEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(EthECKey.GenerateKey().GetPublicAddress()));
            AssertOnChainRejection(underThresholdEx, "AA23", InvalidSignatureSelector, "the guardian quorum length guard (under-threshold rotation)");
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
            var oldOwnerMessages = FlattenMessages(oldOwnerEx);
            Assert.False(oldOwnerEx is Xunit.Sdk.XunitException,
                $"Expected the old owner's signature to be rejected on-chain, but a test assertion threw instead: {oldOwnerEx}");
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
            var removeSessionReceipt = (AATransactionReceipt)await smartSessionService.RemoveSessionRequestAndWaitForReceiptAsync(
                tieredResult.Tier1PermissionId);
            Assert.True(removeSessionReceipt.UserOpSuccess, removeSessionReceipt.FailureDiagnostic);

            var balanceBeforeRevoke = await GetTargetBalanceAsync();
            var revokedEx = await Assert.ThrowsAnyAsync<Exception>(() => tier1Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier1SmallValue }));
            AssertOnChainRejection(revokedEx, "AA23", InvalidPermissionIdSelector, "the revoked tier-1 session");
            Assert.Equal(balanceBeforeRevoke.Value, (await GetTargetBalanceAsync()).Value);

            var accountServiceNewOwner = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountServiceNewOwner.UseAccountAbstraction(newOwnerAccount, client);
            var uninstallConfig = new SmartSessionConfig { ModuleAddress = d.Modules.SmartSession };
            var uninstallReceipt = (AATransactionReceipt)await accountServiceNewOwner.UninstallModuleAndWaitForReceiptAsync(uninstallConfig);
            Assert.True(uninstallReceipt.UserOpSuccess, uninstallReceipt.FailureDiagnostic);

            var accountQueryAfterUninstall = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            Assert.False(await accountQueryAfterUninstall.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()),
                "Expected the SmartSession module to be uninstalled from the account.");

            var treasury = EthECKey.GenerateKey().GetPublicAddress();
            Assert.Equal(BigInteger.Zero, (await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(treasury)).Value);

            var gasReserve = Nethereum.Web3.Web3.Convert.ToWei(0.1m);
            var accountBalanceBeforeSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.True(accountBalanceBeforeSweep.Value > gasReserve,
                $"Expected the account to still hold more than the gas reserve before sweeping, but it held {accountBalanceBeforeSweep.Value}.");
            var sweepAmount = accountBalanceBeforeSweep.Value - gasReserve;

            var sweepReceipt = (AATransactionReceipt)await accountServiceNewOwner.ExecuteAsync(
                new Call { Target = treasury, Value = sweepAmount, Data = Array.Empty<byte>() });
            Assert.True(sweepReceipt.UserOpSuccess, sweepReceipt.FailureDiagnostic);

            Assert.Equal(sweepAmount, (await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(treasury)).Value);
            var accountBalanceAfterSweep = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.True(accountBalanceAfterSweep.Value < gasReserve,
                $"Expected the account balance to drop below the gas reserve after the sweep, but it was {accountBalanceAfterSweep.Value}.");

            var banTxHash = await admin.OffboardBanAsync(UserId, "offboarded");
            Assert.False(string.IsNullOrEmpty(banTxHash));
            Assert.False(await admin.IsActiveAsync(UserId),
                "Expected the account to no longer be Active after the registry ban.");
        }

        private static readonly string PolicyViolationSelector = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);

        private static readonly string InvalidSignatureSelector = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);

        private static readonly string InvalidPermissionIdSelector = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("InvalidPermissionId(bytes32)"))[..4].ToHex(true);

        private Task<Nethereum.Hex.HexTypes.HexBigInteger> GetTargetBalanceAsync() =>
            _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);

        private static void AssertOnChainRejection(Exception ex, string aaErrorCode, string expectedSelector, string reason)
        {
            Assert.False(ex is Xunit.Sdk.XunitException,
                $"Expected an on-chain rejection ({reason}), but a test assertion threw instead: {ex}");

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf(aaErrorCode, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected a {aaErrorCode} validation-revert rejection ({reason}), but got: {ex}");
            Assert.True(messages.IndexOf(expectedSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the selector {expectedSelector} ({reason}) in the rejection, but got: {ex}");
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
