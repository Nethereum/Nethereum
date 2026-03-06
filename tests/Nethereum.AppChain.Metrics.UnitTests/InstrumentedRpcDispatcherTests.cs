using System.Numerics;
using Moq;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AppChain.Metrics.UnitTests
{
    public class InstrumentedRpcDispatcherTests
    {
        private const string ChainId = "420420";
        private readonly RpcMetrics _rpcMetrics;
        private readonly RpcHandlerRegistry _registry;
        private readonly Mock<IChainNode> _mockNode;

        public InstrumentedRpcDispatcherTests()
        {
            _rpcMetrics = new RpcMetrics(ChainId);
            _registry = new RpcHandlerRegistry();
            _mockNode = new Mock<IChainNode>();
            _mockNode.Setup(n => n.GetBlockNumberAsync()).ReturnsAsync(new BigInteger(100));
        }

        [Fact]
        public async Task DispatchAsync_NullRequest_ReturnsInvalidRequestError()
        {
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);

            var response = await dispatcher.DispatchAsync(null!);

            Assert.True(response.HasError);
            Assert.Equal(-32600, response.Error.Code);
        }

        [Fact]
        public async Task DispatchAsync_EmptyMethod_ReturnsInvalidRequestError()
        {
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);
            var request = new RpcRequestMessage(1, "");

            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.HasError);
            Assert.Equal(-32600, response.Error.Code);
        }

        [Fact]
        public async Task DispatchAsync_UnknownMethod_ReturnsMethodNotFoundError()
        {
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);
            var request = new RpcRequestMessage(1, "unknown_method");

            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.HasError);
            Assert.Equal(-32601, response.Error.Code);
        }

        [Fact]
        public async Task DispatchAsync_ValidMethod_IncrementsRequestCounter()
        {
            _registry.AddStandardHandlers();
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);
            var request = new RpcRequestMessage(1, "eth_blockNumber");

            var initialCount = _rpcMetrics.GetRequestCount("eth_blockNumber");

            await dispatcher.DispatchAsync(request);

            var newCount = _rpcMetrics.GetRequestCount("eth_blockNumber");
            Assert.Equal(initialCount + 1, newCount);
        }

        [Fact]
        public async Task DispatchBatchAsync_MultipleRequests_ProcessesAll()
        {
            _registry.AddStandardHandlers();
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);

            var requests = new[]
            {
                new RpcRequestMessage(1, "eth_blockNumber"),
                new RpcRequestMessage(2, "eth_chainId")
            };

            var responses = await dispatcher.DispatchBatchAsync(requests);

            Assert.Equal(2, responses.Count);
        }

        [Fact]
        public async Task DispatchBatchAsync_EmptyArray_ReturnsError()
        {
            var context = new RpcContext(_mockNode.Object, 420420, new MockServiceProvider());
            var dispatcher = new InstrumentedRpcDispatcher(_registry, context, _rpcMetrics);

            var responses = await dispatcher.DispatchBatchAsync(Array.Empty<RpcRequestMessage>());

            Assert.Single(responses);
            Assert.True(responses[0].HasError);
        }
    }

    public class MockServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
