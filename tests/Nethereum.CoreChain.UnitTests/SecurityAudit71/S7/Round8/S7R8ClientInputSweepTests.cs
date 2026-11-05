using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S7.Round8
{
    public class S7R8ClientInputSweepTests
    {
        private const string Escape = "\\" + "ud800";

        private sealed class NodeReached : Exception
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            public List<Exception> Errors { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope();

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (logLevel == LogLevel.Error && exception != null) Errors.Add(exception);
            }

            private sealed class Scope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        private readonly ITestOutputHelper _output;

        public S7R8ClientInputSweepTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static RpcContext Context() => new RpcContext(
            (Func<IChainNode>)(() => throw new NodeReached()),
            new BigInteger(1),
            new ServiceCollection().BuildServiceProvider());

        private static RpcRequestMessage Request(string method, string paramsJson)
        {
            using var document = JsonDocument.Parse(paramsJson);
            return new RpcRequestMessage(1, method) { RawParameters = document.RootElement.Clone() };
        }

        private static readonly string[] Corpus =
        {
            "[5]", "[true]", "[null]", "[{}]", "[[]]", "[\"x\"]", "[\"" + Escape + "\"]",
            "[\"0x0000000000000000000000000000000000000001\",5]",
            "[\"0x0000000000000000000000000000000000000001\",\"" + Escape + "\"]",
            "[\"0x0000000000000000000000000000000000000001\",{\"blockNumber\":5}]",
            "[\"0x0000000000000000000000000000000000000001\",{\"blockHash\":5}]",
            "[{\"fromBlock\":5}]", "[{\"toBlock\":true}]", "[{\"address\":5}]", "[{\"address\":[5]}]",
            "[{\"topics\":[5]}]", "[{\"topics\":[[5]]}]", "[{\"topics\":5}]", "[{\"blockHash\":5}]",
            "[{\"from\":5}]", "[{\"to\":5}]", "[{\"data\":5}]", "[{\"gas\":5}]", "[{\"value\":true}]",
            "[{\"from\":\"" + Escape + "\"}]", "[{\"tracer\":5}]", "[\"0x01\",{\"tracer\":5}]",
            "[\"0xzz\"]", "[\"0x\"]", "[\"0x" + "f" + "ffffffffffffffffffffffffffffffffffffffffffffff\"]",
            "[1e999]", "[-1]", "[1.5]", "[\"0x01\",-1]", "[\"0x01\",1e99]"
        };

        [Fact]
        [Trait("Rule", "S7-R8-02")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task DispatchAsync_WhenAStandardMethodGetsMalformedParams_NeverAnswersInternalErrorForTheClientsInput()
        {
            var registry = new RpcHandlerRegistry().AddStandardHandlers();
            var offenders = new SortedSet<string>();

            foreach (var method in registry.GetAllMethodNames().OrderBy(m => m))
            {
                foreach (var paramsJson in Corpus)
                {
                    var logger = new CapturingLogger();
                    var dispatcher = new RpcDispatcher(registry, Context(), logger);

                    var response = await dispatcher.DispatchAsync(Request(method, paramsJson));

                    if (!response.HasError || response.Error.Code != -32603) continue;
                    var exception = logger.Errors.LastOrDefault();
                    if (exception == null || exception is NodeReached) continue;
                    offenders.Add($"{method} {paramsJson.Replace(Escape, "<lone-surrogate-escape>")} -> {exception?.GetType().Name}: {exception?.Message}");
                }
            }

            foreach (var offender in offenders) _output.WriteLine(offender);
            Assert.Empty(offenders);
        }

        public static IEnumerable<object[]> SingleCases() => new[]
        {
            new object[] { "eth_getLogs", "[{\"fromBlock\":5}]" },
            new object[] { "eth_getLogs", "[{\"toBlock\":true}]" },
            new object[] { "eth_getLogs", "[5]" },
            new object[] { "eth_getLogs", "[\"0x01\"]" },
            new object[] { "eth_newFilter", "[{\"fromBlock\":5}]" },
            new object[] { "eth_getBalance", "[\"0x0000000000000000000000000000000000000001\",\"" + Escape + "\"]" },
            new object[] { "eth_getCode", "[\"0x0000000000000000000000000000000000000001\",\"" + Escape + "\"]" },
            new object[] { "eth_getTransactionCount", "[\"0x0000000000000000000000000000000000000001\",\"" + Escape + "\"]" },
            new object[] { "eth_call", "[{\"gas\":5}]" },
            new object[] { "eth_estimateGas", "[{\"gas\":5}]" },
            new object[] { "txpool_contentFrom", "[null]" }
        };

        [Theory]
        [MemberData(nameof(SingleCases))]
        [Trait("Rule", "S7-R8-02")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task DispatchAsync_WhenAHandlerGetsAParamOfTheWrongTypeOrEncoding_AnswersInvalidParams(string method, string paramsJson)
        {
            var registry = new RpcHandlerRegistry().AddStandardHandlers();
            var dispatcher = new RpcDispatcher(registry, Context(), new CapturingLogger());

            var response = await dispatcher.DispatchAsync(Request(method, paramsJson));

            Assert.True(response.HasError);
            Assert.Equal(-32602, response.Error.Code);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Rule", "S7-R8-01")]
        [Trait("Erc", "JSON-RPC 2.0 section 5.1")]
        public async Task DispatchAsync_WhenDebugLoggingIsOnAndAParamHoldsALoneSurrogateEscape_AnswersAnErrorObjectInsteadOfThrowing(bool instrumented)
        {
            var registry = new RpcHandlerRegistry().AddStandardHandlers();
            var logger = new DebugLogger();
            var request = Request("eth_getBalance", "[\"" + Escape + "\"]");

            RpcResponseMessage response;
            if (instrumented)
            {
                using var metrics = new Nethereum.CoreChain.Metrics.RpcMetrics("1");
                response = await new InstrumentedRpcDispatcher(registry, Context(), metrics, logger).DispatchAsync(request);
            }
            else
            {
                response = await new RpcDispatcher(registry, Context(), logger).DispatchAsync(request);
            }

            Assert.True(response.HasError);
        }

        private sealed class DebugLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope();

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
            }

            private sealed class Scope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
