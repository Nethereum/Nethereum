using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.Rules
{
    public class ValueCapRuleE2ETests : IAsyncLifetime
    {
        private const int CHAIN_ID = 31337;
        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node = null!;
        private IWeb3 _web3 = null!;

        public async Task InitializeAsync()
        {
            var operatorAccount = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
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

        [Theory]
        [Trait("Category", "E2E-Rules")]
        [InlineData(5, 10, true)]
        [InlineData(10, 10, true)]
        [InlineData(11, 10, false)]
        public async Task Given_a_deployed_ValueCapRule_When_evaluated_Then_returns_value_within_cap(
            int value, int cap, bool expectedWithinCap)
        {
            var rule = await ValueCapRuleService.DeployContractAndGetServiceAsync(
                _web3, new ValueCapRuleDeployment());

            var input = new ABIEncode().GetABIEncoded(
                new ABIValue("uint256", (BigInteger)value),
                new ABIValue("uint256", (BigInteger)cap));

            var output = await rule.EvaluateQueryAsync(input);
            var withinCap = new BoolTypeDecoder().Decode(output);

            Assert.Equal(expectedWithinCap, withinCap);
        }

        [Fact]
        [Trait("Category", "E2E-Rules")]
        public async Task Given_large_values_beyond_128_bits_When_evaluated_Then_full_uint256_width_is_honored()
        {
            var rule = await ValueCapRuleService.DeployContractAndGetServiceAsync(
                _web3, new ValueCapRuleDeployment());

            var cap = BigInteger.Pow(2, 200);
            var abi = new ABIEncode();
            var boolDecoder = new BoolTypeDecoder();

            var overCap = boolDecoder.Decode(await rule.EvaluateQueryAsync(
                abi.GetABIEncoded(new ABIValue("uint256", cap + 1), new ABIValue("uint256", cap))));
            var atCap = boolDecoder.Decode(await rule.EvaluateQueryAsync(
                abi.GetABIEncoded(new ABIValue("uint256", cap), new ABIValue("uint256", cap))));

            Assert.False(overCap);
            Assert.True(atCap);
        }
    }
}
