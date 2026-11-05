using System;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S4.Round3
{
    public class MethodNotFoundTextTests
    {
        private static RpcContext Context() => new RpcContext((Func<IChainNode>)(() => null), 1, null);

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnUnregisteredMethodNameOfOneMegabyte_When_RpcDispatcherAnswersIt_Then_TheErrorEchoesOnlyABoundedName()
        {
            var dispatcher = new RpcDispatcher(new RpcHandlerRegistry(), Context());

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, new string('m', 1_000_000)));

            Assert.True(response.HasError);
            Assert.True(response.Error.Message.Length < 200, "the error echoes " + response.Error.Message.Length + " characters of the method name");
        }

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnUnregisteredMethodNameOfOneMegabyte_When_TheInstrumentedDispatcherAnswersIt_Then_TheErrorEchoesOnlyABoundedName()
        {
            using var metrics = new RpcMetrics("1");
            var dispatcher = new InstrumentedRpcDispatcher(new RpcHandlerRegistry(), Context(), metrics);

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, new string('m', 1_000_000)));

            Assert.True(response.HasError);
            Assert.True(response.Error.Message.Length < 200, "the error echoes " + response.Error.Message.Length + " characters of the method name");
        }

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnUnregisteredMethodName_When_TheInstrumentedDispatcherMetersIt_Then_ItIsCountedUnderTheFixedUnknownKey()
        {
            using var metrics = new RpcMetrics("1");
            var dispatcher = new InstrumentedRpcDispatcher(new RpcHandlerRegistry(), Context(), metrics);

            await dispatcher.DispatchAsync(new RpcRequestMessage(1, "eth_doesNotExist"));
            await dispatcher.DispatchAsync(new RpcRequestMessage(2, "eth_doesNotExistEither"));

            Assert.Equal(2, metrics.GetRequestCount(InstrumentedRpcDispatcher.UnknownMethodMetric));
            Assert.Equal(0, metrics.GetRequestCount("eth_doesNotExist"));
        }
    }
}
