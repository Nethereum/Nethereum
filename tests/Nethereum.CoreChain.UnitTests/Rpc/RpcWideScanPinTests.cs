using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.UnitTests.Rpc.TestSupport;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class RpcWideScanPinTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int ChainId = 31337;

        private sealed class NoServices : System.IServiceProvider
        {
            public object GetService(System.Type serviceType) => null;
        }

        private static RpcContext ContextFor(DevChainNode node) => new RpcContext(node, chainId: ChainId, services: new NoServices());

        private static async Task<(DevChainNode Node, byte[] Hash)> NodeWithOneTransactionBlockAsync()
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { ChainId = ChainId });
            await node.StartAsync(new[] { Sender }, BigInteger.Parse("10000000000000000000000"));
            var nonce = await node.GetNonceAsync(Sender);
            await node.SendTransactionAsync(TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, Recipient, 1000, nonce, 1_000_000_000, 21_000, "")));
            await node.MineBlockAsync();
            return (node, await node.GetBlockHashByNumberAsync(1));
        }

        private static async Task<string> BlockJsonAsync(DevChainNode node, RpcRequestMessage request, bool byHash)
        {
            var context = ContextFor(node);
            RpcResponseMessage response = byHash
                ? await new EthGetBlockByHashHandler().HandleAsync(request, context)
                : await new EthGetBlockByNumberHandler().HandleAsync(request, context);
            Assert.False(response.HasError, response.Error?.Message);
            return JsonConvert.SerializeObject(response.ResultNewtonsoft);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_BlockHash_When_GetBlockByHashAndByNumber_Then_ResponsesAreEqual(bool includeTransactions)
        {
            var (node, hash) = await NodeWithOneTransactionBlockAsync();
            using (node)
            {
                var byHash = await BlockJsonAsync(node,
                    new RpcRequestMessage(1, "eth_getBlockByHash", hash.ToHex(true), includeTransactions), true);
                var byNumber = await BlockJsonAsync(node,
                    new RpcRequestMessage(1, "eth_getBlockByNumber", "0x1", includeTransactions), false);

                Assert.Equal(byHash, byNumber);
                Assert.Contains(hash.ToHex(true), byHash);
            }
        }

        [Fact]
        public async Task Given_IncludeTransactions_When_GetBlockByHash_Then_TransactionsCarryBlockHashAndNumber()
        {
            var (node, hash) = await NodeWithOneTransactionBlockAsync();
            using (node)
            {
                var response = await new EthGetBlockByHashHandler().HandleAsync(
                    new RpcRequestMessage(1, "eth_getBlockByHash", hash.ToHex(true), true),
                    ContextFor(node));

                var block = Assert.IsType<BlockWithTransactions>(response.ResultNewtonsoft);
                var tx = Assert.Single(block.Transactions);
                Assert.Equal(hash.ToHex(true), tx.BlockHash);
                Assert.Equal(new BigInteger(1), tx.BlockNumber.Value);
                Assert.Equal(0, (int)tx.TransactionIndex.Value);
            }
        }

        [Fact]
        public async Task Given_AnUnknownBlock_When_GetBlockByHashAndByNumber_Then_ResultIsNull()
        {
            var (node, _) = await NodeWithOneTransactionBlockAsync();
            using (node)
            {
                var context = ContextFor(node);

                var byHash = await new EthGetBlockByHashHandler().HandleAsync(
                    new RpcRequestMessage(1, "eth_getBlockByHash", new string('1', 64).Insert(0, "0x"), false), context);
                var byNumber = await new EthGetBlockByNumberHandler().HandleAsync(
                    new RpcRequestMessage(1, "eth_getBlockByNumber", "0x99", false), context);

                Assert.Null(byHash.ResultNewtonsoft);
                Assert.Null(byNumber.ResultNewtonsoft);
            }
        }

        private static FakeRpcChainNode NodeWithLogsAt(params long[] blockNumbers)
        {
            var node = new FakeRpcChainNode { Latest = blockNumbers[^1] };
            foreach (var blockNumber in blockNumbers)
            {
                node.LogStore.SaveLogsAsync(
                    new List<Log> { Log.Create(new byte[] { 0x01 }, "0x1234567890123456789012345678901234567890") },
                    txHash: new byte[] { (byte)blockNumber, 0x02 },
                    blockHash: new byte[] { (byte)blockNumber, 0x03 },
                    blockNumber: blockNumber,
                    txIndex: 0).GetAwaiter().GetResult();
            }
            return node;
        }

        [Fact]
        public async Task Given_RangeAboveHead_When_GetLogs_Then_ErrorsSameAsGetFilterLogs()
        {
            var node = NodeWithLogsAt(1, 2, 3);
            var context = new RpcContext(node, chainId: 1, services: null);
            var filterId = node.FilterStore.CreateLogFilter(new LogFilter { FromBlock = 1, ToBlock = 9 }, currentBlock: 3);

            var fromGetLogs = await Assert.ThrowsAsync<RpcException>(() => new EthGetLogsHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getLogs", new object[]
                {
                    new Dictionary<string, object> { ["fromBlock"] = "0x1", ["toBlock"] = "0x9" }
                }), context));
            var fromGetFilterLogs = await Assert.ThrowsAsync<RpcException>(() => new EthGetFilterLogsHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context));

            Assert.Equal(fromGetFilterLogs.Message, fromGetLogs.Message);
            Assert.Equal(fromGetFilterLogs.Code, fromGetLogs.Code);
            Assert.Contains("beyond current head", fromGetLogs.Message);
        }

        [Fact]
        public async Task Given_ResultOverCap_When_GetFilterLogs_Then_ResultCapError()
        {
            var node = NodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogResults = 2;
            var filterId = node.FilterStore.CreateLogFilter(new LogFilter { FromBlock = 1, ToBlock = 3 }, currentBlock: 3);

            var error = await Assert.ThrowsAsync<RpcException>(() => new EthGetFilterLogsHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getFilterLogs", filterId), new RpcContext(node, chainId: 1, services: null)));

            Assert.Equal(-32000, error.Code);
            Assert.Contains("more than 2 results", error.Message);
        }

        [Fact]
        public async Task Given_TheSameLogs_When_GetLogsAndGetFilterLogsRun_Then_OnlyGetLogsFillsBlockTimestamps()
        {
            var node = NodeWithLogsAt(1, 2);
            var context = new RpcContext(node, chainId: 1, services: null);
            var filterId = node.FilterStore.CreateLogFilter(new LogFilter { FromBlock = 1, ToBlock = 2 }, currentBlock: 2);

            var viaGetLogs = await new EthGetLogsHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getLogs", new object[]
                {
                    new Dictionary<string, object> { ["fromBlock"] = "0x1", ["toBlock"] = "0x2" }
                }), context);
            var viaFilter = await new EthGetFilterLogsHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context);

            var logs = Assert.IsType<List<FilterLog>>(viaGetLogs.ResultNewtonsoft);
            var filterLogs = Assert.IsType<List<FilterLog>>(viaFilter.ResultNewtonsoft);
            Assert.Equal(2, logs.Count);
            Assert.Equal(logs.Select(l => l.BlockNumber.Value), filterLogs.Select(l => l.BlockNumber.Value));
            Assert.All(logs, l => Assert.NotNull(l.BlockTimestamp));
            Assert.All(filterLogs, l => Assert.Null(l.BlockTimestamp));
        }

        [Fact]
        public async Task Given_NoBlocksInRange_When_FeeHistory_Then_DefaultEntryAppended()
        {
            var node = new FakeRpcChainNode { Latest = 7 };
            node.Config.BaseFee = 1234;

            var response = await new EthFeeHistoryHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_feeHistory", new object[] { "0x0", "latest", new List<double> { 25, 75 } }),
                new RpcContext(node, chainId: 1, services: null));

            var result = Assert.IsType<FeeHistoryResult>(response.ResultNewtonsoft);
            Assert.Equal(new BigInteger(8), result.OldestBlock.Value);
            Assert.Equal(2, result.BaseFeePerGas.Length);
            Assert.Equal(new BigInteger(1234), result.BaseFeePerGas[0].Value);
            Assert.Equal(new[] { 0m }, result.GasUsedRatio);
            Assert.Equal(new[] { 0m }, result.BlobGasUsedRatio);
            Assert.Equal(2, result.BaseFeePerBlobGas.Length);
            Assert.Equal(BigInteger.Zero, result.BaseFeePerBlobGas[0].Value);
            var rewards = Assert.Single(result.Reward);
            Assert.Equal(new BigInteger[] { 0, 0 }, rewards.Select(r => r.Value).ToArray());
        }

        [Fact]
        public async Task Given_NoBlocksInRangeAndNoPercentiles_When_FeeHistory_Then_RewardStaysNull()
        {
            var node = new FakeRpcChainNode { Latest = 7 };
            node.Config.BaseFee = 1234;

            var response = await new EthFeeHistoryHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_feeHistory", new object[] { "0x0", "latest" }),
                new RpcContext(node, chainId: 1, services: null));

            var result = Assert.IsType<FeeHistoryResult>(response.ResultNewtonsoft);
            Assert.Null(result.Reward);
            Assert.Equal(new[] { 0m }, result.GasUsedRatio);
            Assert.Equal(new BigInteger(1234), result.BaseFeePerGas[0].Value);
        }
    }
}
