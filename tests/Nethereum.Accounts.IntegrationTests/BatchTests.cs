using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class BatchTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public BatchTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }
        [Fact]
        public async void ShoulBatchGetBalances()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var batchRequest = new RpcRequestResponseBatch();
            var batchItem1 = new RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger>((EthGetBalance)web3.Eth.GetBalance, web3.Eth.GetBalance.BuildRequest(EthereumClientIntegrationFixture.AccountAddress, BlockParameter.CreateLatest(), 1));
            var batchItem2 = new RpcRequestResponseBatchItem<EthGetBalance, HexBigInteger>((EthGetBalance)web3.Eth.GetBalance, web3.Eth.GetBalance.BuildRequest(EthereumClientIntegrationFixture.AccountAddress, BlockParameter.CreateLatest(), 2));
            batchRequest.BatchItems.Add(batchItem1);
            batchRequest.BatchItems.Add(batchItem2);
            var response = await web3.Client.SendBatchRequestAsync(batchRequest);
            Assert.Equal(batchItem1.Response.Value, batchItem2.Response.Value);
        }


        [Fact]
        public async void ShoulBatchGetBlocks()
        {

            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var batchRequest = new RpcRequestResponseBatch();

            for (int i = 0; i < 10; i++)
            {

                var batchItem1 = new RpcRequestResponseBatchItem<EthGetBlockWithTransactionsHashesByNumber, BlockWithTransactionHashes>((EthGetBlockWithTransactionsHashesByNumber)web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber, web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.BuildRequest(new BlockParameter(new HexBigInteger(i)), i));
                batchRequest.BatchItems.Add(batchItem1);
            }
            var response = await web3.Client.SendBatchRequestAsync(batchRequest);
            Assert.Equal(1438270115, ((RpcRequestResponseBatchItem<EthGetBlockWithTransactionsHashesByNumber, BlockWithTransactionHashes>)response.BatchItems[9]).Response.Timestamp.Value);
        }
    }
}