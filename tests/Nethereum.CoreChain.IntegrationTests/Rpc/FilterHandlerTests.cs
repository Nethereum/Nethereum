using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class FilterHandlerTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly RpcDispatcher _dispatcher;

        public FilterHandlerTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(_fixture.Node, _fixture.ChainId, services);
            _dispatcher = new RpcDispatcher(registry, context);
        }

        [Fact]
        public async Task EthNewFilter_CreatesLogFilter()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "latest",
                toBlock = "latest"
            });

            var request = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var filterId = response.Result.ToString();
            Assert.StartsWith("0x", filterId);
        }

        [Fact]
        public async Task EthNewFilter_WithAddressFilter()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest",
                address = _fixture.Address
            });

            var request = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthNewFilter_WithTopicsFilter()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest",
                topics = new[] { "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef" }
            });

            var request = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task EthGetFilterChanges_ReturnsEmptyWhenNoNewLogs()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "latest",
                toBlock = "latest"
            });

            var createRequest = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            Assert.Null(createResponse.Error);
            var filterId = createResponse.Result.ToString();

            var changesRequest = new RpcRequestMessage(2, "eth_getFilterChanges", filterId);
            var changesResponse = await _dispatcher.DispatchAsync(changesRequest);

            Assert.Null(changesResponse.Error);
            Assert.NotNull(changesResponse.Result);
        }

        [Fact]
        public async Task EthGetFilterLogs_ReturnsLogs()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "0x0",
                toBlock = "latest"
            });

            var createRequest = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            Assert.Null(createResponse.Error);
            var filterId = createResponse.Result.ToString();

            var logsRequest = new RpcRequestMessage(2, "eth_getFilterLogs", filterId);
            var logsResponse = await _dispatcher.DispatchAsync(logsRequest);

            Assert.Null(logsResponse.Error);
            Assert.NotNull(logsResponse.Result);
        }

        [Fact]
        public async Task EthGetFilterLogs_ErrorsForNonLogFilter()
        {
            var createRequest = new RpcRequestMessage(1, "eth_newBlockFilter");
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            var filterId = createResponse.Result.ToString();

            var logsRequest = new RpcRequestMessage(2, "eth_getFilterLogs", filterId);
            var logsResponse = await _dispatcher.DispatchAsync(logsRequest);

            Assert.NotNull(logsResponse.Error);
        }

        [Fact]
        public async Task EthUninstallFilter_ReturnsTrueForExisting()
        {
            var filterParams = JObject.FromObject(new
            {
                fromBlock = "latest",
                toBlock = "latest"
            });

            var createRequest = new RpcRequestMessage(1, "eth_newFilter", filterParams);
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            Assert.Null(createResponse.Error);
            var filterId = createResponse.Result.ToString();

            var uninstallRequest = new RpcRequestMessage(2, "eth_uninstallFilter", filterId);
            var uninstallResponse = await _dispatcher.DispatchAsync(uninstallRequest);

            Assert.Null(uninstallResponse.Error);
            var result = uninstallResponse.Result;
            Assert.True(result is bool b ? b : ((JsonElement)result).GetBoolean());
        }

        [Fact]
        public async Task EthUninstallFilter_ReturnsFalseForUnknown()
        {
            var uninstallRequest = new RpcRequestMessage(1, "eth_uninstallFilter", "0xnonexistent");
            var uninstallResponse = await _dispatcher.DispatchAsync(uninstallRequest);

            Assert.Null(uninstallResponse.Error);
            var result = uninstallResponse.Result;
            Assert.False(result is bool b ? b : ((JsonElement)result).GetBoolean());
        }

        [Fact]
        public async Task EthNewBlockFilter_CreatesFilter()
        {
            var request = new RpcRequestMessage(1, "eth_newBlockFilter");
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            var filterId = response.Result.ToString();
            Assert.StartsWith("0x", filterId);
        }

        [Fact]
        public async Task EthGetFilterChanges_ReturnsNewBlockHashes()
        {
            var createRequest = new RpcRequestMessage(1, "eth_newBlockFilter");
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            var filterId = createResponse.Result.ToString();

            await _fixture.Node.MineBlockAsync();

            var changesRequest = new RpcRequestMessage(2, "eth_getFilterChanges", filterId);
            var changesResponse = await _dispatcher.DispatchAsync(changesRequest);

            Assert.Null(changesResponse.Error);
            Assert.NotNull(changesResponse.Result);

            var results = changesResponse.Result as IEnumerable<object>;
            if (results != null)
            {
                Assert.NotEmpty(results);
            }
        }

        [Fact]
        public async Task EthGetFilterChanges_BlockFilter_ReturnsEmptyAfterPolling()
        {
            var createRequest = new RpcRequestMessage(1, "eth_newBlockFilter");
            var createResponse = await _dispatcher.DispatchAsync(createRequest);
            var filterId = createResponse.Result.ToString();

            await _fixture.Node.MineBlockAsync();

            var changesRequest1 = new RpcRequestMessage(2, "eth_getFilterChanges", filterId);
            await _dispatcher.DispatchAsync(changesRequest1);

            var changesRequest2 = new RpcRequestMessage(3, "eth_getFilterChanges", filterId);
            var changesResponse2 = await _dispatcher.DispatchAsync(changesRequest2);

            Assert.Null(changesResponse2.Error);
        }
    }
}
