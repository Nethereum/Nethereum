using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Enterprise
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "E2E-AppChainModernization")]
    [Trait("UseCase", "EnterpriseTier2TieredApproval")]
    public class EnterpriseTieredApprovalE2ETests
    {
        private const int Threshold = 2;
        private const int SmallCap = 100;
        private const int LargeCap = 1000;
        private const int SmallValue = 60;
        private const int LargeValue = 600;
        private const int OverLargeValue = 1500;

        private readonly AppChainModularBundlerFixture _fixture;

        public EnterpriseTieredApprovalE2ETests(AppChainModularBundlerFixture fixture)
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

        private static List<string> SortedOwnerAddresses(IEnumerable<EthECKey> keys) =>
            MultiGuardianSignatureBlobBuilder.SortAddressesAscending(keys.Select(k => k.GetPublicAddress())).ToList();

        [Fact]
        public async Task Given_an_enrolled_account_with_a_tier1_capped_key_and_a_tier2_Nof3_authority_Then_small_ops_use_the_key_large_ops_need_quorum_and_each_band_rejects_outside_its_gate()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 41 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 41;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());

            var depositSelector = new DepositFunction().GetCallData();

            var operatorKey = EthECKey.GenerateKey();
            var tier1CapInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", _fixture.CapRuleId),
                new ABIValue("uint256", (BigInteger)SmallCap));

            var tier1Salt = new byte[32];
            tier1Salt[31] = 42;

            var tier1SessionConfig = new SmartSessionConfig()
                .WithSessionValidator(d.Modules.EcdsaSessionValidator)
                .WithSessionValidatorInitData(operatorKey.GetPublicAddress())
                .WithSalt(tier1Salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(_fixture.PayableTarget.ContractAddress)
                    .WithSelector(depositSelector)
                    .WithPolicy(_fixture.ValueCapCombinator.ContractAddress, tier1CapInitData)
                    .Build());

            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var sortedOwners = SortedOwnerAddresses(owners);
            var tier2SessionValidatorInitData = new OwnableValidatorConfig(
                _fixture.OwnableValidator.ContractAddress, Threshold, sortedOwners.ToArray()).GetInitData();

            var tier2CapInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", _fixture.CapRuleId),
                new ABIValue("uint256", (BigInteger)LargeCap));

            var tier2Salt = new byte[32];
            tier2Salt[31] = 43;

            var tier2SessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_fixture.OwnableValidator.ContractAddress)
                .WithSessionValidatorInitData(tier2SessionValidatorInitData)
                .WithSalt(tier2Salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(_fixture.PayableTarget.ContractAddress)
                    .WithSelector(depositSelector)
                    .WithPolicy(_fixture.ValueCapCombinator.ContractAddress, tier2CapInitData)
                    .Build());

            var tier1Session = tier1SessionConfig.ToSession();
            var tier2Session = tier2SessionConfig.ToSession();

            var smartSessionQuery = new SmartSessionService(_fixture.OperatorWeb3, d.Modules.SmartSession);
            var tier1PermissionId = await smartSessionQuery.GetPermissionIdQueryAsync(tier1Session);
            var tier2PermissionId = await smartSessionQuery.GetPermissionIdQueryAsync(tier2Session);

            var combinedInitData = ByteUtil.Merge(
                new[] { (byte)SmartSessionMode.UnsafeEnable },
                new ABIEncode().GetABIParamsEncoded(new SessionArrayDto
                {
                    Sessions = new List<Session> { tier1Session, tier2Session }
                }));

            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallModuleRequestAndWaitForReceiptAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, combinedInitData);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);
            Assert.True(await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()));

            var tier1Account = client.GetAccount(
                accountAddress,
                new SmartSessionKeySigningService(operatorKey, tier1PermissionId),
                new SmartSessionValidatorModule(d.Modules.SmartSession, tier1PermissionId));
            var tier1Target = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            tier1Target.UseAccountAbstraction(tier1Account, client);

            var balanceBeforeSmall = await GetTargetBalanceAsync();
            var smallReceipt = (AATransactionReceipt)await tier1Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = SmallValue });
            Assert.True(smallReceipt.UserOpSuccess, smallReceipt.FailureDiagnostic);
            var balanceAfterSmall = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeSmall.Value + SmallValue, balanceAfterSmall.Value);

            var quorumSigningService = new OwnableValidatorSessionSigningService(
                tier2PermissionId, new[] { owners[0], owners[1] }, Threshold);
            var quorumValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tier2PermissionId, signatureSlotCount: Threshold);
            var quorumAccount = client.GetAccount(accountAddress, quorumSigningService, quorumValidatorModule);
            var quorumTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            quorumTarget.UseAccountAbstraction(quorumAccount, client);

            var balanceBeforeLarge = await GetTargetBalanceAsync();
            var largeReceipt = (AATransactionReceipt)await quorumTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = LargeValue });
            Assert.True(largeReceipt.UserOpSuccess, largeReceipt.FailureDiagnostic);
            var balanceAfterLarge = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeLarge.Value + LargeValue, balanceAfterLarge.Value);

            var underQuorumSigningService = new OwnableValidatorSessionSigningService(
                tier2PermissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { owners[0] }, 1));
            var underQuorumValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tier2PermissionId, signatureSlotCount: 1);
            var underQuorumAccount = client.GetAccount(accountAddress, underQuorumSigningService, underQuorumValidatorModule);
            var underQuorumTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            underQuorumTarget.UseAccountAbstraction(underQuorumAccount, client);

            var balanceBeforeUnderQuorum = await GetTargetBalanceAsync();
            var underQuorumEx = await Assert.ThrowsAnyAsync<Exception>(() => underQuorumTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = LargeValue }));
            Assert.False(underQuorumEx is Xunit.Sdk.XunitException,
                $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {underQuorumEx}");

            var underQuorumMessages = FlattenMessages(underQuorumEx);
            Assert.True(underQuorumMessages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (authority), but got: {underQuorumEx}");
            Assert.True(underQuorumMessages.IndexOf(InvalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the CheckSignatures.sol InvalidSignature() selector {InvalidSignatureSelector} (authority) in the rejection, but got: {underQuorumEx}");

            var balanceAfterUnderQuorum = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeUnderQuorum.Value, balanceAfterUnderQuorum.Value);

            var overCapSigningService = new OwnableValidatorSessionSigningService(
                tier2PermissionId, new[] { owners[0], owners[1] }, Threshold);
            var overCapValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tier2PermissionId, signatureSlotCount: Threshold);
            var overCapAccount = client.GetAccount(accountAddress, overCapSigningService, overCapValidatorModule);
            var overCapTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            overCapTarget.UseAccountAbstraction(overCapAccount, client);

            var balanceBeforeOverCap = await GetTargetBalanceAsync();
            var overCapEx = await Assert.ThrowsAnyAsync<Exception>(() => overCapTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = OverLargeValue }));
            Assert.False(overCapEx is Xunit.Sdk.XunitException,
                $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {overCapEx}");

            var overCapMessages = FlattenMessages(overCapEx);
            Assert.True(overCapMessages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (cap policy), but got: {overCapEx}");
            Assert.True(overCapMessages.IndexOf(PolicyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the SmartSession PolicyViolation(bytes32,address) selector {PolicyViolationSelector} (cap policy) in the rejection, but got: {overCapEx}");
            Assert.True(overCapMessages.IndexOf(InvalidSignatureSelector, StringComparison.OrdinalIgnoreCase) < 0,
                $"Expected NO authority InvalidSignature() marker - the cap policy, not the N-of-M authority, must have rejected this op - but got: {overCapEx}");

            var balanceAfterOverCap = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeOverCap.Value, balanceAfterOverCap.Value);

            var balanceBeforeTierBoundary = await GetTargetBalanceAsync();
            var tierBoundaryEx = await Assert.ThrowsAnyAsync<Exception>(() => tier1Target.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = LargeValue }));
            Assert.False(tierBoundaryEx is Xunit.Sdk.XunitException,
                $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {tierBoundaryEx}");

            var tierBoundaryMessages = FlattenMessages(tierBoundaryEx);
            Assert.True(tierBoundaryMessages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (tier-1 cap policy), but got: {tierBoundaryEx}");
            Assert.True(tierBoundaryMessages.IndexOf(PolicyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the SmartSession PolicyViolation(bytes32,address) selector {PolicyViolationSelector} (tier-1 cap policy) in the rejection, but got: {tierBoundaryEx}");

            var balanceAfterTierBoundary = await GetTargetBalanceAsync();
            Assert.Equal(balanceBeforeTierBoundary.Value, balanceAfterTierBoundary.Value);
        }

        private static readonly string PolicyViolationSelector = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);

        private static readonly string InvalidSignatureSelector = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);

        private Task<Nethereum.Hex.HexTypes.HexBigInteger> GetTargetBalanceAsync() =>
            _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);

        private static string FlattenMessages(Exception ex)
        {
            var builder = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }

        [FunctionOutput]
        private class SessionArrayDto
        {
            [Parameter("tuple[]", "sessions", 1)]
            public virtual List<Session> Sessions { get; set; }
        }
    }
}
