using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    internal sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly List<(string Category, string Message)> _entries;

            public CapturingLogger(string category, List<(string Category, string Message)> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_entries) _entries.Add((_category, formatter(state, exception)));
            }
        }
    }

    [Collection("Sequential")]
    public class DevChainRpcHostTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string LoopContract = "0x1000000000000000000000000000000000000001";
        private const string LoopOverride = "{\"" + LoopContract + "\":{\"code\":\"0x5b600056\"}}";

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        private static string ErrorMessage(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("message").GetString() ?? "" : "";
        }

        private static async Task MineAsync(DevChainRpcHost host, int blocks)
        {
            for (var i = 0; i < blocks; i++) await host.CallAsync("evm_mine", "[]");
        }

        [Fact]
        [Trait("Finding", "S4-R1-03")]
        public async Task Given_ABrowserFormPostWithTextPlainContentType_When_ItIsSentToADevChain_Then_ItIsRefusedAsUnsupportedMediaType()
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var result = await host.PostAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", "text/plain");

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, result.Status);
        }

        [Fact]
        [Trait("Finding", "S4-R1-09")]
        public async Task Given_ABatchOfMillionsOfEmptyMembers_When_ItIsPostedWithinTheBodyLimit_Then_ItIsRejectedWithoutMaterialisingEveryMember()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var members = 3_000_000;
            var body = "[" + string.Join(",", Enumerable.Repeat("{}", members)) + "]";
            Assert.True(body.Length < 10 * 1024 * 1024);

            GC.Collect();
            var before = GC.GetTotalAllocatedBytes(true);
            var result = await host.PostAsync(body);
            var allocated = GC.GetTotalAllocatedBytes(true) - before;

            Assert.True(allocated < 120L * 1024 * 1024,
                $"a {body.Length / 1024 / 1024} MiB batch of {members} members allocated {allocated / 1024 / 1024} MiB before the batch limit was applied (status {(int)result.Status})");
        }

        [Fact]
        [Trait("Finding", "S4-R1-10")]
        public async Task Given_AFeeHistoryRequestWithTwentyThousandPercentiles_When_ItIsSent_Then_ItIsRejectedAsOverTheQueryLimit()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            await MineAsync(host, 5);
            var percentiles = string.Join(",", Enumerable.Range(0, 20_000).Select(i => (i / 200.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));

            var body = await host.CallAsync("eth_feeHistory", "[\"0x5\",\"latest\",[" + percentiles + "]]");

            Assert.NotEqual(0, ErrorCode(body));
        }

        [Fact]
        [Trait("Finding", "S4-R1-11")]
        public async Task Given_SimulatedCallsWhoseExplicitGasSumsPastTheRpcGasCap_When_SimulateV1RunsThem_Then_TheTotalGasUsedStaysWithinTheCap()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0xF00000\"}";
            var calls = string.Join(",", Enumerable.Repeat(call, 4));
            var simulation = "{\"blockStateCalls\":[{\"stateOverrides\":" + LoopOverride + ",\"calls\":[" + calls + "]}]}";

            var body = await host.CallAsync("eth_simulateV1", "[" + simulation + ",\"latest\"]");

            using var document = JsonDocument.Parse(body);
            BigInteger total = 0;
            if (document.RootElement.TryGetProperty("result", out var blocks))
            {
                foreach (var block in blocks.EnumerateArray())
                    foreach (var callResult in block.GetProperty("calls").EnumerateArray())
                        total += callResult.GetProperty("gasUsed").GetString()!.HexToBigInteger(false);
            }

            Assert.True(total <= 50_000_000, $"four loop calls burned {total} gas against an RpcGasCap of 50,000,000");
        }

        [Fact]
        [Trait("Finding", "S4-R1-12")]
        public async Task Given_ACallTraceOfAnEndlessLoopAtTheRpcGasCap_When_DebugTraceCallRuns_Then_ItIsAbortedWithinTheTraceTimeout()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0x2faf080\"}";

            var stopwatch = Stopwatch.StartNew();
            await host.CallAsync("debug_traceCall",
                "[" + call + ",\"latest\",{\"tracer\":\"callTracer\",\"stateOverrides\":" + LoopOverride + "}]");
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
                $"a 50M gas trace ran for {stopwatch.Elapsed.TotalSeconds:0.0} s with no timeout (reference: 5 s)");
        }

        [Fact]
        [Trait("Finding", "S4-R1-13")]
        public async Task Given_ABlockRangeCapOfFive_When_AFilterStartingAtGenesisIsPolledForChanges_Then_TheRangeCapAppliesToThePoll()
        {
            await using var host = await DevChainRpcHost.StartAsync(config => config.Node.Rpc.MaxLogBlockRange = 5);
            await MineAsync(host, 20);
            var direct = await host.CallAsync("eth_getLogs", "[{\"fromBlock\":\"0x0\",\"toBlock\":\"latest\"}]");
            Assert.Equal(-32000, ErrorCode(direct));

            var created = await host.CallAsync("eth_newFilter", "[{\"fromBlock\":\"0x0\"}]");
            using var createdDocument = JsonDocument.Parse(created);
            var filterId = createdDocument.RootElement.GetProperty("result").GetString();
            await MineAsync(host, 20);
            var changes = await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]");

            Assert.Equal(-32000, ErrorCode(changes));
        }

        [Fact]
        [Trait("Finding", "S4-R1-14")]
        public async Task Given_AnotherClientsFilter_When_AStrangerUninstallsTheFirstFortyFilterIds_Then_TheOwnersFilterStillWorks()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var created = await host.CallAsync("eth_newBlockFilter", "[]");
            using var createdDocument = JsonDocument.Parse(created);
            var filterId = createdDocument.RootElement.GetProperty("result").GetString();

            for (var i = 1; i <= 40; i++)
                await host.CallAsync("eth_uninstallFilter", "[\"0x" + i.ToString("x") + "\"]");

            var poll = await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]");
            Assert.Equal(0, ErrorCode(poll));
        }

        [Fact]
        [Trait("Finding", "S4-R1-15")]
        public async Task Given_AMethodNameWithALineBreakAndAHundredKilobytes_When_ItIsPosted_Then_TheServerLogsNoUnboundedOrMultiLineRequestText()
        {
            var logs = new CapturingLoggerProvider();
            await using var host = await DevChainRpcHost.StartAsync(logProvider: logs);
            var forged = "eth_chainId\\n[12:00:00] fail: Nethereum.Fake[0] forged admin login" + new string('x', 100_000);

            await host.PostAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + forged + "\",\"params\":[]}");
            await host.PostAsync("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + forged + "\",\"params\":[]}]");

            List<(string Category, string Message)> rpcEntries;
            lock (logs.Entries) rpcEntries = logs.Entries.Where(e => e.Category == "Nethereum.DevChain.Rpc").ToList();
            Assert.NotEmpty(rpcEntries);
            Assert.All(rpcEntries, entry =>
            {
                Assert.DoesNotContain('\n', entry.Message);
                Assert.True(entry.Message.Length < 512, $"a log entry carried {entry.Message.Length} chars of request text");
            });
        }

        [Theory]
        [Trait("Finding", "S4-R1-16")]
        [InlineData("debug_traceCall", "[{\"gas\":\"zz\"},\"latest\"]")]
        [InlineData("debug_traceCall", "[{\"to\":\"zz\"},\"latest\"]")]
        [InlineData("debug_traceCall", "[{},\"latest\",5]")]
        [InlineData("debug_traceTransaction", "[\"0x1234567890123456789012345678901234567890123456789012345678901234\",5]")]
        public async Task Given_ClientInputThatCannotBeParsed_When_ATraceMethodRejectsIt_Then_ItAnswersInvalidParamsWithoutFrameworkExceptionText(string method, string parameters)
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var body = await host.CallAsync(method, parameters);

            Assert.Equal(-32602, ErrorCode(body));
            Assert.DoesNotContain("byte array", ErrorMessage(body));
            Assert.DoesNotContain("requested operation", ErrorMessage(body));
        }

        [Fact]
        [Trait("Finding", "S4-R1-18")]
        public async Task Given_AThousandMemberBatchWhoseAnswersTogetherExceedTwentyFiveMegabytes_When_ItIsPosted_Then_TheResponseIsCapped()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            var code = "0x" + new string('6', 2 * 24_000);
            await host.CallAsync("hardhat_setCode", "[\"" + LoopContract + "\",\"" + code + "\"]");
            var member = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getCode\",\"params\":[\"" + LoopContract + "\",\"latest\"]}";
            var batch = "[" + string.Join(",", Enumerable.Repeat(member, 1000)) + "]";

            var result = await host.PostAsync(batch);

            Assert.True(result.Body.Length <= 25_000_000,
                $"one 100 KB request produced a {result.Body.Length / 1_000_000} MB response");
        }
    }
}
