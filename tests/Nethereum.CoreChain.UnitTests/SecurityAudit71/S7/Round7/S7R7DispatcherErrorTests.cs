using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S7.Round7
{
    public class S7R7DispatcherErrorTests
    {
        private const string Secret = "Host=10.0.0.5;Password=hunter2";

        private sealed class ThrowingHandler : RpcHandlerBase
        {
            public override string MethodName => "test_throw";

            public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context) =>
                throw new InvalidOperationException(Secret);
        }

        private static RpcContext Context() => new RpcContext(
            (Func<IChainNode>)(() => throw new InvalidOperationException("no node")),
            new BigInteger(1),
            new ServiceCollection().BuildServiceProvider());

        [Fact]
        [Trait("Rule", "S7-R7-10")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task DispatchAsync_WhenAHandlerThrowsAnUnexpectedException_AnswersInternalErrorWithoutTheExceptionText()
        {
            var registry = new RpcHandlerRegistry();
            registry.Register(new ThrowingHandler());
            var dispatcher = new RpcDispatcher(registry, Context());

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, "test_throw"));

            Assert.True(response.HasError);
            Assert.DoesNotContain(Secret, response.Error.Message);
        }

        [Fact]
        [Trait("Rule", "S7-R7-10")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task InstrumentedDispatchAsync_WhenAHandlerThrowsAnUnexpectedException_AnswersInternalErrorWithoutTheExceptionText()
        {
            var registry = new RpcHandlerRegistry();
            registry.Register(new ThrowingHandler());
            using var metrics = new RpcMetrics("1");
            var dispatcher = new InstrumentedRpcDispatcher(registry, Context(), metrics);

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, "test_throw"));

            Assert.True(response.HasError);
            Assert.DoesNotContain(Secret, response.Error.Message);
        }
    }
}
