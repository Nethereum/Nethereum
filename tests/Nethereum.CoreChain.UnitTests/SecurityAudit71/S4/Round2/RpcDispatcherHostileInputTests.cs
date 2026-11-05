using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S4.Round2
{
    public class RpcDispatcherHostileInputTests
    {
        private sealed class RecordingLogger : ILogger
        {
            public List<(LogLevel Level, Exception Exception)> Entries { get; } = new List<(LogLevel, Exception)>();

            public IDisposable BeginScope<TState>(TState state) => new Scope();

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                lock (Entries) Entries.Add((logLevel, exception));
            }

            private sealed class Scope : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class RejectingHandler : IRpcHandler
        {
            public string MethodName => "eth_getBalance";

            public Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context) =>
                throw RpcException.InvalidParams("invalid params");
        }

        private static InstrumentedRpcDispatcher NewDispatcher(RpcMetrics metrics, ILogger logger = null)
        {
            var registry = new RpcHandlerRegistry();
            registry.Register(new RejectingHandler());
            return new InstrumentedRpcDispatcher(
                registry, new RpcContext((Func<IChainNode>)(() => null), 1, null), metrics, logger);
        }

        [Fact]
        [Trait("Finding", "S4-R2-04")]
        public async Task Given_ManyRequestsWithDistinctUnregisteredMethodNames_When_TheyAreDispatched_Then_TheMetricsDoNotRetainEveryName()
        {
            using var metrics = new RpcMetrics("1");
            var dispatcher = NewDispatcher(metrics);
            var names = await DispatchDistinctNamesAsync(dispatcher, 100);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            var retained = names.Count(name => name.TryGetTarget(out _));

            Assert.True(retained < 40,
                $"100 requests with 200,000 character method names left {retained} of the names retained by the dispatcher metrics");
            GC.KeepAlive(metrics);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<List<WeakReference<string>>> DispatchDistinctNamesAsync(InstrumentedRpcDispatcher dispatcher, int count)
        {
            var padding = new string('m', 200_000);
            var names = new List<WeakReference<string>>();
            for (var i = 0; i < count; i++)
            {
                var name = i.ToString("D4") + padding;
                names.Add(new WeakReference<string>(name));
                await dispatcher.DispatchAsync(new RpcRequestMessage(i, name));
            }
            return names;
        }

        [Fact]
        [Trait("Finding", "S4-R2-05")]
        public async Task Given_ARequestRejectedForInvalidParams_When_TheDispatcherAnswersIt_Then_NoErrorLevelEntryWithAStackTraceIsWritten()
        {
            using var metrics = new RpcMetrics("1");
            var logger = new RecordingLogger();
            var dispatcher = NewDispatcher(metrics, logger);

            var response = await dispatcher.DispatchAsync(new RpcRequestMessage(1, "eth_getBalance"));

            Assert.True(response.HasError);
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error && entry.Exception != null);
        }
    }
}
