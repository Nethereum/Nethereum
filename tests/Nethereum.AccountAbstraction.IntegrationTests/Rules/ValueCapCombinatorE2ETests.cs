using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule;
using Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Util;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.Rules
{
    public class ValueCapCombinatorE2ETests : IAsyncLifetime
    {
        private const int CHAIN_ID = 31337;
        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const uint VALIDATION_SUCCESS = 0;
        private const uint VALIDATION_FAILED = 1;

        private DevChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private string _operatorAddress = null!;

        public async Task InitializeAsync()
        {
            var operatorAccount = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
            _operatorAddress = operatorAccount.Address;
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { operatorAccount.Address }, Web3.Web3.Convert.ToWei(10000));
            _web3 = _node.CreateWeb3(operatorAccount);
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        private static byte[] Id(string label) =>
            Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(label));

        private async Task<(ValueCapCombinatorService combinator, byte[] capRuleId)> DeployAndRegisterAsync()
        {
            var registry = await RuleRegistryService.DeployContractAndGetServiceAsync(_web3, new RuleRegistryDeployment());
            var valueCapRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(_web3, new ValueCapRuleDeployment());

            var capRuleId = Id("value-cap");
            await registry.RegisterRuleRequestAndWaitForReceiptAsync(capRuleId, valueCapRuleReceipt.ContractAddress);

            var combinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
                _web3, new ValueCapCombinatorDeployment { Registry = registry.ContractAddress });

            return (combinator, capRuleId);
        }

        [Theory]
        [Trait("Category", "E2E-Rules")]
        [InlineData(5, 10, VALIDATION_SUCCESS)]
        [InlineData(10, 10, VALIDATION_SUCCESS)]
        [InlineData(11, 10, VALIDATION_FAILED)]
        public async Task Given_an_initialized_combinator_When_checkAction_is_called_Then_it_dispatches_through_the_registry(
            int value, int cap, uint expectedValidationResult)
        {
            var (combinator, capRuleId) = await DeployAndRegisterAsync();
            var configId = Id("configId-value-cap");
            var account = _operatorAddress;

            var initData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", capRuleId),
                new ABIValue("uint256", (BigInteger)cap));
            await combinator.InitializeWithMultiplexerRequestAndWaitForReceiptAsync(account, configId, initData);

            var result = await combinator.CheckActionQueryAsync(
                configId, account, account, (BigInteger)value, System.Array.Empty<byte>());

            Assert.Equal((BigInteger)expectedValidationResult, result);
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_an_uninitialized_configId_and_account_When_checkAction_is_called_Then_it_reverts()
        {
            var (combinator, _) = await DeployAndRegisterAsync();
            var neverInitializedConfigId = Id("never-initialized");
            var account = _operatorAddress;

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                combinator.CheckActionQueryAsync(
                    neverInitializedConfigId, account, account, (BigInteger)1, System.Array.Empty<byte>()));

            Assert.True(ex.IsCustomErrorFor<NotInitializedError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_an_unregistered_capRuleId_When_initializeWithMultiplexer_is_called_Then_it_reverts()
        {
            var (combinator, _) = await DeployAndRegisterAsync();
            var configId = Id("configId-unknown-rule");
            var account = _operatorAddress;
            var unregisteredRuleId = Id("never-registered-with-this-combinator");

            var initData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", unregisteredRuleId),
                new ABIValue("uint256", (BigInteger)10));

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                combinator.InitializeWithMultiplexerRequestAndWaitForReceiptAsync(account, configId, initData));

            Assert.True(ex.IsCustomErrorFor<Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition.UnknownRuleError>());
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_a_rule_that_returns_malformed_output_When_checkAction_is_called_Then_it_reverts_InvalidRuleOutput()
        {
            var registry = await RuleRegistryService.DeployContractAndGetServiceAsync(_web3, new RuleRegistryDeployment());
            var badOutputRuleReceipt = await BadOutputRuleService.DeployContractAndWaitForReceiptAsync(_web3, new BadOutputRuleDeployment());

            var capRuleId = Id("bad-output-rule");
            await registry.RegisterRuleRequestAndWaitForReceiptAsync(capRuleId, badOutputRuleReceipt.ContractAddress);

            var combinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
                _web3, new ValueCapCombinatorDeployment { Registry = registry.ContractAddress });

            var configId = Id("configId-bad-output-rule");
            var account = _operatorAddress;

            var initData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", capRuleId),
                new ABIValue("uint256", (BigInteger)10));
            await combinator.InitializeWithMultiplexerRequestAndWaitForReceiptAsync(account, configId, initData);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                combinator.CheckActionQueryAsync(
                    configId, account, account, (BigInteger)5, System.Array.Empty<byte>()));

            Assert.True(ex.IsCustomErrorFor<InvalidRuleOutputError>());
        }
    }
}
