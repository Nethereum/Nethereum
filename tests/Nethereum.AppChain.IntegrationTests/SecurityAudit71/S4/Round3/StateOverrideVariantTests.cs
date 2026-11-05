using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class StateOverrideVariantTests
    {
        private const string A = "\"0x0000000000000000000000000000000000000001\"";
        private const string Slot = "\"0x0000000000000000000000000000000000000000000000000000000000000001\"";
        private readonly ITestOutputHelper _out;

        public StateOverrideVariantTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Overrides =
        {
            "{" + A + ":{\"balance\":\"0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"}}",
            "{" + A + ":{\"balance\":\"-1\"}}",
            "{" + A + ":{\"balance\":5}}",
            "{" + A + ":{\"nonce\":\"0xffffffffffffffffffffffff\"}}",
            "{" + A + ":{\"nonce\":\"0xffffffffffffffff\"}}",
            "{" + A + ":{\"code\":\"0xzz\"}}",
            "{" + A + ":{\"code\":\"0x\"}}",
            "{" + A + ":{\"code\":null}}",
            "{" + A + ":{\"state\":{" + Slot + ":" + Slot + "},\"stateDiff\":{" + Slot + ":" + Slot + "}}}",
            "{" + A + ":{\"state\":{\"0x01\":\"0x01\"}}}",
            "{" + A + ":{\"state\":{" + Slot + ":\"0x\"}}}",
            "{" + A + ":{\"state\":[]}}",
            "{" + A + ":{\"movePrecompileToAddress\":" + A + "}}",
            "{" + A + ":{\"movePrecompileToAddress\":\"0x0000000000000000000000000000000000000002\"},\"0x0000000000000000000000000000000000000002\":{\"movePrecompileToAddress\":" + A + "}}",
            "{" + A + ":{\"movePrecompileToAddress\":\"zz\"}}",
            "{" + A + ":null}",
            "{" + A + ":5}",
            "{\"zz\":{}}",
            "{\"0x\":{}}",
            "{" + A + ":{\"unknownField\":1}}",
            "[]", "5", "\"x\"", "null",
        };

        private static readonly string[] Methods = { "eth_call", "debug_traceCall", "eth_simulateV1", "eth_estimateGas", "eth_createAccessList" };

        [Fact]
        [Trait("Finding", "S4-R3-01")]
        public async Task Probe_StateOverrideShapes_NeverAnswerInternalErrorOrHang()
        {
            await using var host = await DevChainFuzzHost.StartAsync();
            var offenders = new List<string>();
            var total = 0;
            foreach (var method in Methods)
            foreach (var overrides in Overrides)
            {
                total++;
                string parameters = method switch
                {
                    "eth_simulateV1" => "[{\"blockStateCalls\":[{\"stateOverrides\":" + overrides + ",\"calls\":[{\"to\":" + A + "}]}]},\"latest\"]",
                    "debug_traceCall" => "[{\"to\":" + A + "},\"latest\",{\"stateOverrides\":" + overrides + "}]",
                    _ => "[{\"to\":" + A + "},\"latest\"," + overrides + "]",
                };
                var sw = Stopwatch.StartNew();
                string body;
                try { body = await host.CallAsync(method, parameters); }
                catch (Exception ex) { body = ex.GetType().Name; }
                sw.Stop();
                if (sw.ElapsedMilliseconds > 3000 || body.Contains("-32603") || !body.StartsWith("{"))
                    offenders.Add(method + " " + overrides + " -> " + sw.ElapsedMilliseconds + "ms " + (body.Length > 140 ? body.Substring(0, 140) : body));
            }

            List<(Microsoft.Extensions.Logging.LogLevel Level, string Category, string Message, Exception? Exception)> entries;
            lock (host.Log.Entries) entries = host.Log.Entries.ToList();
            _out.WriteLine("requests: " + total + " offenders: " + offenders.Count);
            foreach (var o in offenders) _out.WriteLine("OFFENDER " + o);
            foreach (var g in entries.Where(e => e.Exception != null).GroupBy(e => e.Level + " " + e.Exception!.GetType().Name + " " + e.Message.Split(':')[0]))
                _out.WriteLine("LOG " + g.Count() + "x " + g.Key);
            Assert.Empty(offenders);
        }
    }
}
