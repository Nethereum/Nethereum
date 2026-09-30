using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.UnitTests.Rpc.TestSupport;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class FinalityLabelResolutionTests
    {
        private const string Address = "0x1234567890123456789012345678901234567890";

        private static RpcContext BuildContext(FakeRpcChainNode node, StubFinalityCursorProvider cursor)
        {
            var services = cursor == null ? null : new SingleServiceProvider(cursor);
            return new RpcContext(node, chainId: 1, services: services);
        }

        [Theory]
        [InlineData("finalized")]
        [InlineData("safe")]
        public async Task ResolveBlockNumberAsync_FinalizedOrSafe_UsesCursor_NotHead(string tag)
        {
            var node = new FakeRpcChainNode { Latest = 100 };
            var cursor = new StubFinalityCursorProvider { Finalized = 42, Safe = 55 };
            var context = BuildContext(node, cursor);
            var handler = new EthGetBalanceHandler();

            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBalance", Address, tag), context);

            var expected = tag == "finalized" ? cursor.Finalized!.Value : cursor.Safe!.Value;
            var actual = ((HexBigInteger)response.ResultNewtonsoft).Value;
            Assert.Equal(expected, actual);
            Assert.NotEqual(node.Latest, actual);
        }

        [Theory]
        [InlineData("finalized")]
        [InlineData("safe")]
        public async Task ResolveBlockNumberAsync_LatestOnlyCursor_FallsBackToLatest(string tag)
        {
            var node = new FakeRpcChainNode { Latest = 100 };
            var cursor = new StubFinalityCursorProvider();
            var context = BuildContext(node, cursor);
            var handler = new EthGetBalanceHandler();

            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBalance", Address, tag), context);

            var actual = ((HexBigInteger)response.ResultNewtonsoft).Value;
            Assert.Equal(node.Latest, actual);
        }

        [Theory]
        [InlineData("latest")]
        [InlineData("pending")]
        public async Task ResolveBlockNumberAsync_LatestOrPending_ResolvesToHead(string tag)
        {
            var node = new FakeRpcChainNode { Latest = 100 };
            var context = BuildContext(node, new StubFinalityCursorProvider { Finalized = 42, Safe = 55 });
            var handler = new EthGetBalanceHandler();

            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBalance", Address, tag), context);

            var actual = ((HexBigInteger)response.ResultNewtonsoft).Value;
            Assert.Equal(node.Latest, actual);
        }

        [Fact]
        public async Task ResolveBlockNumberAsync_Earliest_ResolvesToZero()
        {
            var node = new FakeRpcChainNode { Latest = 100 };
            var context = BuildContext(node, new StubFinalityCursorProvider { Finalized = 42, Safe = 55 });
            var handler = new EthGetBalanceHandler();

            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBalance", Address, "earliest"), context);

            var actual = ((HexBigInteger)response.ResultNewtonsoft).Value;
            Assert.Equal(BigInteger.Zero, actual);
        }

        [Fact]
        public async Task ResolveBlockNumberAsync_NoServices_DoesNotThrow_LatestStillResolves()
        {
            var node = new FakeRpcChainNode { Latest = 7 };
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetBalanceHandler();

            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, "eth_getBalance", Address, "latest"), context);

            var actual = ((HexBigInteger)response.ResultNewtonsoft).Value;
            Assert.Equal(node.Latest, actual);
        }

        [Fact]
        public async Task GetLogs_ToBlockFinalized_ClampsToFinalizedNumber_NotHead()
        {
            var node = new FakeRpcChainNode { Latest = 10 };
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 0;
            var log = Log.Create(new byte[] { 0x01 }, Address);
            node.LogStore.SaveLogsAsync(
                new System.Collections.Generic.List<Log> { log },
                txHash: new byte[] { 0x04, 0x02 }, blockHash: new byte[] { 0x04, 0x03 },
                blockNumber: 4, txIndex: 0).GetAwaiter().GetResult();
            node.LogStore.SaveLogsAsync(
                new System.Collections.Generic.List<Log> { log },
                txHash: new byte[] { 0x0A, 0x02 }, blockHash: new byte[] { 0x0A, 0x03 },
                blockNumber: 10, txIndex: 0).GetAwaiter().GetResult();

            var cursor = new StubFinalityCursorProvider { Finalized = 4 };
            var context = BuildContext(node, cursor);
            var handler = new EthGetLogsHandler();

            var filter = new System.Collections.Generic.Dictionary<string, object>
            {
                ["fromBlock"] = "0x0",
                ["toBlock"] = "finalized"
            };
            var response = await handler.HandleAsync(new RpcRequestMessage(1, "eth_getLogs", new object[] { filter }), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            var single = Assert.Single(result);
            Assert.Equal(4, single.BlockNumber.Value);
        }

        [Fact]
        public void FinalityLabelledEthGetBlockByNumberHandler_ResolveLabel_IsTheReusedSource()
        {
            var cursor = new StubFinalityCursorProvider { Finalized = 42, Safe = 55 };
            BigInteger latest = 100;

            Assert.Equal(42, FinalityLabelledEthGetBlockByNumberHandler.ResolveLabel("finalized", cursor, latest));
            Assert.Equal(55, FinalityLabelledEthGetBlockByNumberHandler.ResolveLabel("safe", cursor, latest));
            Assert.Equal(latest, FinalityLabelledEthGetBlockByNumberHandler.ResolveLabel("latest", cursor, latest));
            Assert.Equal(latest, FinalityLabelledEthGetBlockByNumberHandler.ResolveLabel("pending", cursor, latest));
            Assert.Equal(0, FinalityLabelledEthGetBlockByNumberHandler.ResolveLabel("earliest", cursor, latest));
        }
    }
}
