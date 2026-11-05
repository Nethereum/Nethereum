using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class BlockTagVariantTests
    {
        private readonly ITestOutputHelper _out;

        public BlockTagVariantTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Tags =
        {
            "\"pending\"", "\"safe\"", "\"finalized\"", "\"earliest\"", "\"0x0\"", "\"0x1\"", "\"0xffffffffffffffff\"",
            "\"0x7fffffffffffffffffffffffffff\"", "\"-1\"", "\"LATEST\"", "\"latest \"", "\"\"", "null", "5", "-1", "1.5",
            "{\"blockHash\":\"0x0000000000000000000000000000000000000000000000000000000000000000\"}",
            "{\"blockNumber\":\"0x1\"}", "{\"blockNumber\":\"0x1\",\"requireCanonical\":true}", "{}", "{\"blockNumber\":\"0xzz\"}",
            "{\"blockHash\":\"0x12\"}", "{\"blockHash\":5}",
        };

        private static readonly string[] Templates =
        {
            "[\"0x0000000000000000000000000000000000000001\",{TAG}]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\"},{TAG}]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\",\"data\":\"0x\"},{TAG},{}]",
            "[{TAG},false]",
            "[\"0x0000000000000000000000000000000000000001\",\"0x0\",{TAG}]",
            "[\"0x0000000000000000000000000000000000000001\",[\"0x0\"],{TAG}]",
            "[{\"blocks\":[{}]},{TAG}]",
        };

        private static readonly string[] Methods =
        {
            "eth_call", "eth_estimateGas", "eth_createAccessList", "eth_getBalance", "eth_getCode", "eth_getStorageAt", "eth_getTransactionCount",
            "eth_getBlockByNumber", "eth_getBlockTransactionCountByNumber", "eth_getBlockReceipts", "eth_getProof", "eth_simulateV1",
            "eth_getBlockAccessList", "debug_traceCall", "debug_traceBlockByNumber", "eth_getStorageValues", "eth_feeHistory",
        };

        [Fact]
        [Trait("Finding", "S4-R3-sound")]
        public async Task Probe_BlockTagAndStateReadVariants_NeverAnswerInternalErrorOrHang()
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            var offenders = new List<string>();
            var total = 0;
            foreach (var method in Methods)
            foreach (var template in Templates)
            foreach (var tag in Tags)
            {
                total++;
                var sw = Stopwatch.StartNew();
                string body;
                try { body = await host.CallAsync(method, template.Replace("{TAG}", tag)); }
                catch (Exception ex) { body = ex.GetType().Name; }
                sw.Stop();
                if (sw.ElapsedMilliseconds > 3000 || body.Contains("-32603") || body.Contains("Internal error") || !body.StartsWith("{"))
                    offenders.Add(method + " " + template.Replace("{TAG}", tag) + " -> " + sw.ElapsedMilliseconds + "ms " + (body.Length > 120 ? body.Substring(0, 120) : body));
            }

            _out.WriteLine("requests: " + total + " offenders: " + offenders.Count);
            foreach (var o in offenders) _out.WriteLine("OFFENDER " + o);
            Assert.Empty(offenders);
        }
    }
}
