using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class MultiSendTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public MultiSendTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class MultiSendDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "60808060405234601557610180908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c638d80ff0a14610024575f80fd5b60203660031901126100b55760043567ffffffffffffffff81116100b557366023820112156100b557806004013567ffffffffffffffff81116100b95760405190601f8101601f19908116603f0116820167ffffffffffffffff8111838210176100b95760405280825236602482850101116100b5576020815f9260246100b3960183860137830101526100cd565b005b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b8051906020809201915b8281106100e357505050565b6055818301805160f81c600182015160601c916015810151603582015194859201905f9390815f14610139575060011461012a575b50509050156100b557016055016100d7565b5f938493505af480825f610118565b90505f948594505af180825f61011856fea2646970667358221220fa38e6e72d4ea5fe1bf36934cec859c202a7ce7226cb92f9fbe9e4afbeee4edc64736f6c634300081c0033";

            public MultiSendDeployment() : base(BYTECODE) { }
        }

        public class CounterDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "6080806040523460135760dc908160188239f35b5f80fdfe608060405260043610156010575f80fd5b5f3560e01c806303df179c14608657806306661abd14606c5763d09de08a146036575f80fd5b346068575f3660031901126068575f545f1981146054576001015f55005b634e487b7160e01b5f52601160045260245ffd5b5f80fd5b346068575f36600319011260685760205f54604051908152f35b3460685760203660031901126068575f5460043581018091116054575f5500fea26469706673582212201b76f1a070fdb2c9ffe422c6b51de62dadbb89435f2035a5cca483912868d5b064736f6c634300081c0033";

            public CounterDeployment() : base(BYTECODE) { }
        }

        [Function("increment")]
        public class IncrementFunction : FunctionMessage { }

        [Function("incrementBy")]
        public class IncrementByFunction : FunctionMessage
        {
            [Parameter("uint256", "amount", 1)]
            public BigInteger Amount { get; set; }
        }

        [Function("count", "uint256")]
        public class CountFunction : FunctionMessage { }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "multicall", "MultiSend: batch multiple write transactions", SkillName = "multicall", Order = 3)]
        [NethereumDocExample(DocSection.DeFi, "gnosis-safe", "MultiSend: batch multiple actions in one transaction", Order = 1)]
        public async Task ShouldEncodeAndExecuteMultiSend()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var multiSendReceipt = await web3.Eth.GetContractDeploymentHandler<MultiSendDeployment>()
                .SendRequestAndWaitForReceiptAsync(new MultiSendDeployment()).ConfigureAwait(false);
            var multiSendAddress = multiSendReceipt.ContractAddress;

            var counterReceipt = await web3.Eth.GetContractDeploymentHandler<CounterDeployment>()
                .SendRequestAndWaitForReceiptAsync(new CounterDeployment()).ConfigureAwait(false);
            var counterAddress = counterReceipt.ContractAddress;

            var input1 = new MultiSendFunctionInput<IncrementFunction>(
                new IncrementFunction(), counterAddress);

            var input2 = new MultiSendFunctionInput<IncrementByFunction>(
                new IncrementByFunction { Amount = 5 }, counterAddress);

            var multiSendFunction = new MultiSendFunction(input1, input2);

            var transactionHandler = web3.Eth.GetContractTransactionHandler<MultiSendFunction>();
            var receipt = await transactionHandler.SendRequestAndWaitForReceiptAsync(
                multiSendAddress, multiSendFunction).ConfigureAwait(false);

            Assert.False(receipt.HasErrors());

            var contractHandler = web3.Eth.GetContractHandler(counterAddress);
            var count = await contractHandler.QueryAsync<CountFunction, BigInteger>().ConfigureAwait(false);
            Assert.Equal(6, count);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "multicall", "MultiSendEncoder: encode packed transactions", SkillName = "multicall", Order = 4)]
        [NethereumDocExample(DocSection.DeFi, "gnosis-safe", "MultiSendEncoder: encode packed transactions for Safe", Order = 2)]
        public void ShouldEncodeMultiSendTransactions()
        {
            var counterAddress = "0x0000000000000000000000000000000000000001";

            var input1 = new MultiSendFunctionInput<IncrementFunction>(
                new IncrementFunction(), counterAddress);

            var input2 = new MultiSendFunctionInput<IncrementByFunction>(
                new IncrementByFunction { Amount = 10 }, counterAddress);

            var encoded = MultiSendEncoder.EncodeMultiSendList(input1, input2);
            Assert.True(encoded.Length > 0);

            var multiSendFunction = new MultiSendFunction(input1, input2);
            Assert.NotNull(multiSendFunction.Transactions);
            Assert.True(multiSendFunction.Transactions.Length > 0);
        }
    }
}
