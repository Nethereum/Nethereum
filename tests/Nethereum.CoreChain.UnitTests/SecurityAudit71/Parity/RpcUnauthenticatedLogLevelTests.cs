using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.Parity
{
    public class RpcUnauthenticatedLogLevelTests
    {
        private sealed class RecordingLogger : ILogger
        {
            public List<LogLevel> Levels { get; } = new List<LogLevel>();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                lock (Levels) Levels.Add(logLevel);
            }
        }

        private static RpcContext Context() => new RpcContext((Func<IChainNode>)(() => null), 1, null);

        [Fact]
        [Trait("Finding", "P7")]
        public async Task Given_AnUnregisteredMethod_When_RpcDispatcherAnswersIt_Then_NothingIsLoggedAboveDebug()
        {
            var logger = new RecordingLogger();
            var dispatcher = new RpcDispatcher(new RpcHandlerRegistry(), Context(), logger);

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, "eth_doesNotExist"));

            Assert.Equal(-32601, response.Error.Code);
            Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Information);
        }

        [Fact]
        [Trait("Finding", "P7")]
        public async Task Given_AnUnregisteredMethod_When_TheInstrumentedDispatcherAnswersIt_Then_NothingIsLoggedAboveDebug()
        {
            var logger = new RecordingLogger();
            using var metrics = new RpcMetrics("1");
            var dispatcher = new InstrumentedRpcDispatcher(new RpcHandlerRegistry(), Context(), metrics, logger);

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, "eth_doesNotExist"));

            Assert.Equal(-32601, response.Error.Code);
            Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Information);
        }

        [Fact]
        [Trait("Finding", "P7")]
        public async Task Given_AnUnregisteredMethodOverHttp_When_TheEnvelopeAnswersIt_Then_NothingIsLoggedAboveDebug()
        {
            var logger = new RecordingLogger();
            var dispatcher = new RpcDispatcher(new RpcHandlerRegistry(), Context(), logger);
            var envelope = new JsonRpcEnvelope(dispatcher, JsonRpcEnvelope.CreateSerializerOptions(), logger);

            var result = await envelope.HandleAsync(
                Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_doesNotExist\",\"params\":[]}"),
                CancellationToken.None);

            Assert.Contains("-32601", result.Body);
            Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Information);
        }
    }
}
