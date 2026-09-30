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
    public class EthGetLogsCapsTests
    {
        private static readonly string Address = "0x1234567890123456789012345678901234567890";

        private static FakeRpcChainNode BuildNodeWithLogsAt(params long[] blockNumbers)
        {
            var node = new FakeRpcChainNode { Latest = blockNumbers.Length > 0 ? blockNumbers[^1] : 0 };
            foreach (var blockNumber in blockNumbers)
            {
                var log = Log.Create(new byte[] { 0x01 }, Address);
                node.LogStore.SaveLogsAsync(
                    new System.Collections.Generic.List<Log> { log },
                    txHash: new byte[] { (byte)blockNumber, 0x02 },
                    blockHash: new byte[] { (byte)blockNumber, 0x03 },
                    blockNumber: blockNumber,
                    txIndex: 0).GetAwaiter().GetResult();
            }
            return node;
        }

        private static RpcRequestMessage GetLogsRequest(object fromBlock = null, object toBlock = null)
        {
            var filter = new System.Collections.Generic.Dictionary<string, object>();
            if (fromBlock != null) filter["fromBlock"] = fromBlock;
            if (toBlock != null) filter["toBlock"] = toBlock;
            return new RpcRequestMessage(1, "eth_getLogs", new object[] { filter });
        }

        [Fact]
        public async Task GetLogs_RangeWithinCap_ReturnsMatchingLogs()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3, 4, 5);
            node.Config.RpcMaxLogBlockRange = 10;
            node.Config.RpcMaxLogResults = 10;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var response = await handler.HandleAsync(GetLogsRequest("0x1", "0x5"), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task GetLogs_RangeExceedsCap_ThrowsRpcException()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 10;
            node.Config.RpcMaxLogResults = 0;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var ex = await Assert.ThrowsAsync<RpcException>(
                () => handler.HandleAsync(GetLogsRequest("0x0", "0x14"), context));

            Assert.Contains("too many blocks", ex.Message);
        }

        [Fact]
        public async Task GetLogs_RangeCapZero_Disabled_NoRangeLimit()
        {
            var node = BuildNodeWithLogsAt(1, 500_000);
            node.Latest = 1_000_000;
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 0;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var response = await handler.HandleAsync(GetLogsRequest("0x0", "0xF4240"), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetLogs_ResultCountExceedsCap_ThrowsRpcException()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 2;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var ex = await Assert.ThrowsAsync<RpcException>(
                () => handler.HandleAsync(GetLogsRequest("0x0", "0x3"), context));

            Assert.Contains("more than 2 results", ex.Message);
        }

        [Fact]
        public async Task GetLogs_ResultCountWithinCap_ReturnsAll()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 10;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var response = await handler.HandleAsync(GetLogsRequest("0x0", "0x3"), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetLogs_ResultCapZero_Disabled_NoResultLimit()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3, 4, 5);
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 0;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var response = await handler.HandleAsync(GetLogsRequest("0x0", "0x5"), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task GetLogs_NoRangeSpecified_DefaultsToSingleLatestBlock()
        {
            var node = BuildNodeWithLogsAt(3, 5);
            node.Latest = 5;
            node.Config.RpcMaxLogBlockRange = 10;
            node.Config.RpcMaxLogResults = 10;
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            var response = await handler.HandleAsync(GetLogsRequest(), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            var single = Assert.Single(result);
            Assert.Equal(new HexBigInteger(5).Value, single.BlockNumber.Value);
        }

        [Fact]
        public async Task GetLogs_FromBlockGreaterThanToBlock_ThrowsInvalidParams()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetLogsHandler();

            await Assert.ThrowsAsync<RpcException>(
                () => handler.HandleAsync(GetLogsRequest("0xA", "0x5"), context));
        }


        [Fact]
        public async Task GetFilterLogs_RangeExceedsCap_ThrowsRpcException()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 10;
            node.Config.RpcMaxLogResults = 0;
            var filterId = node.FilterStore.CreateLogFilter(
                new LogFilter { FromBlock = 0, ToBlock = 20 }, currentBlock: 3);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetFilterLogsHandler();

            var ex = await Assert.ThrowsAsync<RpcException>(
                () => handler.HandleAsync(new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context));

            Assert.Contains("too many blocks", ex.Message);
        }

        [Fact]
        public async Task GetFilterLogs_ResultCountExceedsCap_ThrowsRpcException()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 2;
            var filterId = node.FilterStore.CreateLogFilter(
                new LogFilter { FromBlock = 0, ToBlock = 3 }, currentBlock: 3);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetFilterLogsHandler();

            var ex = await Assert.ThrowsAsync<RpcException>(
                () => handler.HandleAsync(new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context));

            Assert.Contains("more than 2 results", ex.Message);
        }

        [Fact]
        public async Task GetFilterLogs_InRange_Unaffected()
        {
            var node = BuildNodeWithLogsAt(1, 2, 3);
            node.Config.RpcMaxLogBlockRange = 10;
            node.Config.RpcMaxLogResults = 10;
            var filterId = node.FilterStore.CreateLogFilter(
                new LogFilter { FromBlock = 0, ToBlock = 3 }, currentBlock: 3);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetFilterLogsHandler();

            var response = await handler.HandleAsync(new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetFilterLogs_CapsZero_Disabled_NoLimit()
        {
            var node = BuildNodeWithLogsAt(1, 500_000);
            node.Config.RpcMaxLogBlockRange = 0;
            node.Config.RpcMaxLogResults = 0;
            var filterId = node.FilterStore.CreateLogFilter(
                new LogFilter { FromBlock = 0, ToBlock = 500_000 }, currentBlock: 500_000);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetFilterLogsHandler();

            var response = await handler.HandleAsync(new RpcRequestMessage(1, "eth_getFilterLogs", filterId), context);

            var result = Assert.IsType<System.Collections.Generic.List<FilterLog>>(response.ResultNewtonsoft);
            Assert.Equal(2, result.Count);
        }
    }
}
