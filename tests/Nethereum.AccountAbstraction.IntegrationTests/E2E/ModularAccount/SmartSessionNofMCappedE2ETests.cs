using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(SmartSessionPolicyBundlerCollection.COLLECTION_NAME)]
    [Trait("Category", "E2E-SmartSession")]
    [Trait("UseCase", "SessionKeyNofMAuthorityAndCap")]
    public class SmartSessionNofMCappedE2ETests
    {
        private const int Threshold = 2;
        private const int Cap = 100;
        private const int WithinCapValue = 60;
        private const int OverCapValue = 150;

        private readonly SmartSessionPolicyBundlerFixture _fixture;

        public SmartSessionNofMCappedE2ETests(SmartSessionPolicyBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var infra = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                _fixture.FactoryService.ContractAddress,
                _fixture.EcdsaValidatorService.ContractAddress,
                VerifyingPaymasterAddress: string.Empty);

            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(infra)
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        private static List<string> SortedAddresses(params EthECKey[] keys) =>
            MultiGuardianSignatureBlobBuilder.SortAddressesAscending(keys.Select(k => k.GetPublicAddress())).ToList();

        private static byte[] Id(string label) =>
            Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(label));

        private async Task<(ValueCapCombinatorService combinator, byte[] capRuleId, PayableTargetService payableTarget)>
            DeployCapStackAsync()
        {
            var registry = await RuleRegistryService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new RuleRegistryDeployment());
            var valueCapRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(
                _fixture.Bootstrap.OperatorWeb3, new ValueCapRuleDeployment());

            var capRuleId = Id("value-cap-r1c-b");
            await registry.RegisterRuleRequestAndWaitForReceiptAsync(capRuleId, valueCapRuleReceipt.ContractAddress);

            var combinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new ValueCapCombinatorDeployment { Registry = registry.ContractAddress });

            var payableTarget = await PayableTargetService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new PayableTargetDeployment());

            return (combinator, capRuleId, payableTarget);
        }

        private async Task<(IAAClient client, NethereumSmartAccount account, PayableTargetService payableTarget, byte[] permissionId)>
            SetUpAccountWithNofMSessionAndCapAsync(byte saltByte, int threshold, IReadOnlyList<EthECKey> owners)
        {
            var client = BuildClient();
            var (combinator, capRuleId, payableTarget) = await DeployCapStackAsync();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var counter = new TestCounterService(
                _fixture.Bootstrap.OperatorWeb3, _fixture.TestCounterService.ContractHandler.ContractAddress);
            counter.UseAccountAbstraction(account, client);
            var deployReceipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(deployReceipt.UserOpSuccess, deployReceipt.FailureDiagnostic);

            var sortedOwners = SortedAddresses(owners.ToArray());
            var sessionValidatorInitData = new OwnableValidatorConfig(
                _fixture.OwnableValidatorService.ContractAddress, threshold, sortedOwners.ToArray()).GetInitData();

            var salt = new byte[32];
            salt[31] = saltByte;

            var capConfigInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", capRuleId),
                new ABIValue("uint256", (BigInteger)Cap));

            var depositSelector = new DepositFunction().GetCallData();

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_fixture.OwnableValidatorService.ContractAddress)
                .WithSessionValidatorInitData(sessionValidatorInitData)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(payableTarget.ContractAddress)
                    .WithSelector(depositSelector)
                    .WithPolicy(combinator.ContractAddress, capConfigInitData)
                    .Build());

            var session = sessionConfig.ToSession();
            var permissionId = await _fixture.SmartSessionService.GetPermissionIdQueryAsync(session);

            sessionConfig.ModuleAddress = _fixture.SmartSessionService.ContractAddress;
            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);
            Assert.True(await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _fixture.SmartSessionService.ContractAddress, Array.Empty<byte>()));

            return (client, account, payableTarget, permissionId);
        }

        private static IAccountSigningService BuildNofMSigningService(byte[] permissionId, Func<byte[], byte[]> buildBlob) =>
            new OwnableValidatorSessionSigningService(permissionId, buildBlob);

        private IErc7579ValidatorModule BuildValidatorModule(byte[] permissionId, int estimationSlotCount) =>
            new OwnableValidatorSessionValidatorModule(_fixture.SmartSessionService.ContractAddress, permissionId, estimationSlotCount);

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthorityAndCap")]
        public async Task Given_2of3_quorum_met_And_value_within_cap_Then_the_session_userOp_executes_and_the_target_balance_increases()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };

            var (client, account, payableTarget, permissionId) =
                await SetUpAccountWithNofMSessionAndCapAsync(1, Threshold, owners);

            var signingService = new OwnableValidatorSessionSigningService(permissionId, new[] { owners[0], owners[1] }, Threshold);
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: Threshold);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            payableTarget.UseAccountAbstraction(sessionAccount, client);

            var balanceBefore = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);

            var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapValue });

            Assert.True(receipt.UserOpSuccess, receipt.FailureDiagnostic);
            var balanceAfter = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);
            Assert.Equal(balanceBefore.Value + WithinCapValue, balanceAfter.Value);
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthorityAndCap")]
        public async Task Given_only_1of3_members_And_value_within_cap_Then_the_session_userOp_is_rejected_by_the_authority()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };

            var (client, account, payableTarget, permissionId) =
                await SetUpAccountWithNofMSessionAndCapAsync(2, Threshold, owners);

            var signingService = BuildNofMSigningService(
                permissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { owners[0] }, 1));
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: 1);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            payableTarget.UseAccountAbstraction(sessionAccount, client);

            var balanceBefore = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapValue }));
            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (authority), but got: {ex}");

            var invalidSignatureSelector = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(invalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the CheckSignatures.sol InvalidSignature() selector {invalidSignatureSelector} (authority) in the rejection, but got: {ex}");

            var balanceAfter = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);
            Assert.Equal(balanceBefore.Value, balanceAfter.Value);
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthorityAndCap")]
        public async Task Given_2of3_quorum_met_And_value_over_cap_Then_the_session_userOp_is_rejected_by_the_combinator()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };

            var (client, account, payableTarget, permissionId) =
                await SetUpAccountWithNofMSessionAndCapAsync(3, Threshold, owners);

            var signingService = new OwnableValidatorSessionSigningService(permissionId, new[] { owners[0], owners[1] }, Threshold);
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: Threshold);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            payableTarget.UseAccountAbstraction(sessionAccount, client);

            var balanceBefore = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = OverCapValue }));
            Assert.False(ex is Xunit.Sdk.XunitException, $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {ex}");

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (cap policy), but got: {ex}");

            var policyViolationSelector = Sha3Keccack.Current.CalculateHash(
                Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(policyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the SmartSession PolicyViolation(bytes32,address) selector {policyViolationSelector} (cap policy) in the rejection, but got: {ex}");

            var invalidSignatureSelector = Sha3Keccack.Current.CalculateHash(
                Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(invalidSignatureSelector, StringComparison.OrdinalIgnoreCase) < 0,
                $"Expected NO authority InvalidSignature() marker - the cap policy, not the N-of-M authority, must have rejected this op - but got: {ex}");

            var balanceAfter = await _fixture.Bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(payableTarget.ContractAddress);
            Assert.Equal(balanceBefore.Value, balanceAfter.Value);
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
