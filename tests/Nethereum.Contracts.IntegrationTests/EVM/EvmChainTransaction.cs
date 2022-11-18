using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmChainTransaction
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EvmChainTransaction(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldRetrieveTransctionFromChainAndSimulateIt()
        {
            //Scenario of complex uniswap clone to run end to end a previous transaction and see logs etc
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync("0xb9f4e6e5c90329a43da70ced8e8974c3fa34e67e32283bfa82778296fa79dd98");
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(txn.BlockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(txn.To); // runtime code;
            var instructions = ProgramInstructionsUtils.GetProgramInstructions(code);
            var txnInput = txn.ConvertToTransactionInput();
            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(new HexBigInteger(txn.BlockNumber.Value - 1)));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(txnInput, executionStateService, null, (long)txn.BlockNumber.Value, (long)block.Timestamp.Value);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            var trace = await evmSimulator.ExecuteAsync(program, 0, 0, true);
            Assert.True(program.ProgramResult.Logs.Count == 23);
        }
    }
}