using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Util;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.Rules
{
    public class RuleRegistryE2ETests : IAsyncLifetime
    {
        private const int CHAIN_ID = 31337;
        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string NON_OWNER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        private DevChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private IWeb3 _nonOwnerWeb3 = null!;

        public async Task InitializeAsync()
        {
            var operatorAccount = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
            var nonOwnerAccount = new Web3Account(NON_OWNER_PRIVATE_KEY, CHAIN_ID);
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(
                new[] { operatorAccount.Address, nonOwnerAccount.Address }, Web3.Web3.Convert.ToWei(10000));
            _web3 = _node.CreateWeb3(operatorAccount);
            _nonOwnerWeb3 = _node.CreateWeb3(nonOwnerAccount);
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        private static byte[] RuleId(string label) =>
            Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(label));

        private async Task<(RuleRegistryService registry, string valueCapRuleAddress)> DeployRegistryAndValueCapRuleAsync()
        {
            var registry = await RuleRegistryService.DeployContractAndGetServiceAsync(_web3, new RuleRegistryDeployment());
            var valueCapRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(_web3, new ValueCapRuleDeployment());
            return (registry, valueCapRuleReceipt.ContractAddress);
        }

        [Theory]
        [Trait("Category", "E2E-Rules")]
        [InlineData(5, 10, true)]
        [InlineData(10, 10, true)]
        [InlineData(11, 10, false)]
        public async Task Given_a_registered_ValueCapRule_When_executed_via_registry_Then_dispatch_returns_the_rule_decision(
            int value, int cap, bool expectedWithinCap)
        {
            var (registry, valueCapRuleAddress) = await DeployRegistryAndValueCapRuleAsync();
            var ruleId = RuleId("value-cap");

            await registry.RegisterRuleRequestAndWaitForReceiptAsync(ruleId, valueCapRuleAddress);

            var input = new ABIEncode().GetABIEncoded(
                new ABIValue("uint256", (BigInteger)value),
                new ABIValue("uint256", (BigInteger)cap));

            var output = await registry.ExecuteQueryAsync(ruleId, input);
            var withinCap = new BoolTypeDecoder().Decode(output);

            Assert.Equal(expectedWithinCap, withinCap);
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_registration_closed_When_registering_a_new_rule_Then_it_reverts_but_the_prior_registration_stands()
        {
            var (registry, valueCapRuleAddress) = await DeployRegistryAndValueCapRuleAsync();
            var controlRuleId = RuleId("value-cap-control");
            var lateRuleId = RuleId("value-cap-late");

            await registry.RegisterRuleRequestAndWaitForReceiptAsync(controlRuleId, valueCapRuleAddress);
            var registeredAddress = await registry.RuleOfQueryAsync(controlRuleId);
            Assert.Equal(valueCapRuleAddress.ToLowerInvariant(), registeredAddress.ToLowerInvariant());

            await registry.CloseRegistrationRequestAndWaitForReceiptAsync();
            var closed = await registry.RegistrationClosedQueryAsync();
            Assert.True(closed);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                registry.RegisterRuleRequestAndWaitForReceiptAsync(lateRuleId, valueCapRuleAddress));

            Assert.True(ex.IsCustomErrorFor<RegistrationIsClosedError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_an_unregistered_rule_id_When_executed_Then_it_reverts()
        {
            var (registry, _) = await DeployRegistryAndValueCapRuleAsync();
            var unregisteredRuleId = RuleId("never-registered");

            var input = new ABIEncode().GetABIEncoded(
                new ABIValue("uint256", (BigInteger)1),
                new ABIValue("uint256", (BigInteger)1));

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                registry.ExecuteQueryAsync(unregisteredRuleId, input));

            Assert.True(ex.IsCustomErrorFor<UnknownRuleError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_an_already_set_rule_id_When_registered_again_Then_it_reverts()
        {
            var (registry, valueCapRuleAddress) = await DeployRegistryAndValueCapRuleAsync();
            var ruleId = RuleId("value-cap-immutable");

            await registry.RegisterRuleRequestAndWaitForReceiptAsync(ruleId, valueCapRuleAddress);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                registry.RegisterRuleRequestAndWaitForReceiptAsync(ruleId, valueCapRuleAddress));

            Assert.True(ex.IsCustomErrorFor<RuleAlreadySetError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_a_non_owner_When_registering_a_rule_Then_it_reverts()
        {
            var (registry, valueCapRuleAddress) = await DeployRegistryAndValueCapRuleAsync();
            var nonOwnerRegistry = new RuleRegistryService(_nonOwnerWeb3, registry.ContractAddress);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                nonOwnerRegistry.RegisterRuleRequestAndWaitForReceiptAsync(RuleId("intruder"), valueCapRuleAddress));

            Assert.True(ex.IsCustomErrorFor<NotOwnerError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_a_non_owner_When_closing_registration_Then_it_reverts()
        {
            var (registry, _) = await DeployRegistryAndValueCapRuleAsync();
            var nonOwnerRegistry = new RuleRegistryService(_nonOwnerWeb3, registry.ContractAddress);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                nonOwnerRegistry.CloseRegistrationRequestAndWaitForReceiptAsync());

            Assert.True(ex.IsCustomErrorFor<NotOwnerError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_a_zero_rule_address_When_registering_Then_it_reverts()
        {
            var (registry, _) = await DeployRegistryAndValueCapRuleAsync();

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                registry.RegisterRuleRequestAndWaitForReceiptAsync(
                    RuleId("zero"), "0x0000000000000000000000000000000000000000"));

            Assert.True(ex.IsCustomErrorFor<ZeroRuleError>());
        }
    }
}
