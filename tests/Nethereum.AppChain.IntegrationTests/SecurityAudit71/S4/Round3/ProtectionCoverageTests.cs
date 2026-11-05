using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class ProtectionCoverageTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string LoopContract = "0x1000000000000000000000000000000000000001";
        private const string LoopCode = "0x5b600056";
        private const string LoopOverride = "{\"" + LoopContract + "\":{\"code\":\"" + LoopCode + "\"}}";
        private readonly ITestOutputHelper _out;

        public ProtectionCoverageTests(ITestOutputHelper output) { _out = output; }

        private static int ErrorCode(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetProperty("code").GetInt32() : 0;
        }

        private static string CountedLoop(int iterations) =>
            "0x62" + iterations.ToString("x6") + "5b8015" + "6011" + "57" + "600190" + "03" + "6004" + "56" + "5b00";

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnEndlessLoopAndAShortTraceTimeout_When_DebugTraceCallRunsIt_Then_ItIsAbortedWithinTheTimeout()
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            host.Node.Config.RpcTraceTimeout = TimeSpan.FromMilliseconds(1);
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0x2faf080\"}";

            var body = await host.CallAsync("debug_traceCall",
                "[" + call + ",\"latest\",{\"tracer\":\"callTracer\",\"stateOverrides\":" + LoopOverride + "}]");

            Assert.Contains("execution aborted", body);
        }

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnEndlessLoopAndAShortRpcEvmTimeout_When_SimulateV1RunsIt_Then_ItIsAbortedWithinTheTimeout()
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            host.Node.Config.RpcEvmTimeout = TimeSpan.FromMilliseconds(1);
            var request = "{\"blockStateCalls\":[{\"stateOverrides\":" + LoopOverride + ",\"calls\":[{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0x2faf080\"}]}]}";

            var body = await host.CallAsync("eth_simulateV1", "[" + request + ",\"latest\"]");

            Assert.Contains("execution aborted", body);
        }

        [Theory]
        [InlineData("debug_traceCall", "{\"disableStack\":\"x\"}")]
        [InlineData("debug_traceCall", "{\"limit\":\"abc\"}")]
        [InlineData("debug_traceCall", "{\"tracerConfig\":5}")]
        [InlineData("debug_traceTransaction", "{\"disableStack\":\"x\"}")]
        [InlineData("debug_traceTransaction", "{\"limit\":\"abc\"}")]
        [InlineData("debug_traceTransaction", "{\"tracerConfig\":5}")]
        [InlineData("debug_traceBlockByNumber", "{\"disableStack\":\"x\"}")]
        [InlineData("debug_traceBlockByNumber", "{\"limit\":\"abc\"}")]
        [InlineData("debug_traceBlockByNumber", "{\"tracerConfig\":5}")]
        [InlineData("debug_traceBlockByHash", "{\"disableStack\":\"x\"}")]
        [InlineData("debug_traceBlockByHash", "{\"limit\":\"abc\"}")]
        [InlineData("debug_traceBlockByHash", "{\"tracerConfig\":5}")]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AWronglyTypedTraceConfig_When_ItIsRejected_Then_TheAnswerIsInvalidParams(string method, string config)
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            var hash = "\"0x0000000000000000000000000000000000000000000000000000000000000001\"";
            var parameters = method switch
            {
                "debug_traceCall" => "[{\"to\":\"0x0000000000000000000000000000000000000001\"},\"latest\"," + config + "]",
                "debug_traceBlockByNumber" => "[\"latest\"," + config + "]",
                _ => "[" + hash + "," + config + "]",
            };

            var body = await host.CallAsync(method, parameters);

            Assert.Equal(-32602, ErrorCode(body));
        }

        [Fact]
        [Trait("Finding", "S4-R3-coverage")]
        public async Task Given_AnEstimateWhoseProbesEachFitTheTimeoutButTogetherDoNot_When_EthEstimateGasSearches_Then_TheWholeSearchSharesOneDeadline()
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            await host.CallAsync("hardhat_setCode", "[\"" + LoopContract + "\",\"" + CountedLoop(390_000) + "\"]");
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\"}";

            await host.CallAsync("eth_estimateGas", "[" + call + ",\"latest\"]");
            var clock = Stopwatch.StartNew();
            var fastest = long.MaxValue;
            for (var sample = 0; sample < 7; sample++)
            {
                clock.Restart();
                var unbounded = await host.CallAsync("eth_estimateGas", "[" + call + ",\"latest\"]");
                fastest = Math.Min(fastest, clock.ElapsedMilliseconds);
                _out.WriteLine("estimate without a tight timeout: " + clock.ElapsedMilliseconds + " ms " + unbounded);
            }

            host.Node.Config.RpcEvmTimeout = TimeSpan.FromMilliseconds(Math.Max(60, fastest / 2));
            clock.Restart();
            var body = await host.CallAsync("eth_estimateGas", "[" + call + ",\"latest\"]");
            _out.WriteLine("estimate with timeout " + host.Node.Config.RpcEvmTimeout.TotalMilliseconds + " ms: " + clock.ElapsedMilliseconds + " ms " + body);

            Assert.Contains("execution aborted", body);
        }
    }
}
