using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class HostileParamsTests
    {
        private readonly ITestOutputHelper _out;

        public HostileParamsTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Shapes =
        {
            "[]", "[null]", "[null,null]", "[1]", "[\"x\"]", "[\"0x\"]", "[\"0xzz\"]", "[{}]", "[[]]", "[true]", "[-1]",
            "[\"0x1\",\"0x2\"]", "[{\"to\":5}]", "[{\"from\":\"0x1\",\"to\":\"0x2\",\"data\":5}]", "[[],[]]", "[\"latest\"]",
            "[{},\"latest\"]", "[\"0x0000000000000000000000000000000000000000\",\"latest\"]",
            "[\"0x0000000000000000000000000000000000000000\",{},\"latest\"]", "{}", "\"str\"", "[1e400]",
            "[\"0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"]",
            "[{\"fromBlock\":\"x\"}]", "[{\"fromBlock\":1}]", "[{\"topics\":[5]}]", "[{\"address\":5}]", "[{\"blockHash\":\"0x12\"}]",
            "[99999999999999999999999999]", "[\"0x0000000000000000000000000000000000000000\",[\"0x1\"],\"latest\"]",
            "[\"0x0000000000000000000000000000000000000000\",\"0x\",\"latest\"]",
            "[\"latest\",true]", "[\"latest\",5]", "[\"0x1\",\"0x1\"]", "[{\"to\":\"0x00\"}]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\",\"data\":\"0xzz\"},\"latest\"]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\",\"gas\":\"x\"},\"latest\"]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\",\"value\":\"-1\"},\"latest\"]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\"},\"latest\",5]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\"},\"latest\",{\"0x00\":{}}]",
            "[{\"to\":\"0x0000000000000000000000000000000000000001\"},\"latest\",{\"0x0000000000000000000000000000000000000001\":5}]",
        };

        private static readonly string[] Skip =
        {
            "testing_buildBlockV1", "testing_commitBlockV1", "hardhat_mine", "anvil_mine", "evm_mine", "evm_increaseTime", "evm_setNextBlockTimestamp", "hardhat_setHead", "debug_setHead",
            "anvil_setBlockTimestampInterval", "evm_setBlockTimestampInterval", "evm_setIntervalMining", "anvil_setIntervalMining",
            "hardhat_setLoggingEnabled", "anvil_setLoggingEnabled", "evm_snapshot", "evm_revert",
        };

        [Fact]
        [Trait("Finding", "S4-R3-01")]
        public async Task Probe_EveryRegisteredMethod_WithHostileParams_NeverAnswersInternalError()
        {
            await using var host = await DevChainFuzzHost.StartAsync();
            var registry = host.Services.GetRequiredService<RpcHandlerRegistry>();
            var offenders = new List<string>();
            var slow = new List<string>();
            var total = 0;

            foreach (var method in registry.GetAllMethodNames().OrderBy(n => n).ToList())
            {
                if (Skip.Contains(method, StringComparer.OrdinalIgnoreCase)) continue;
                foreach (var shape in Shapes)
                {
                    total++;
                    var sw = Stopwatch.StartNew();
                    string body;
                    int status;
                    try
                    {
                        var r = await host.PostAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":" + shape + "}");
                        body = r.Body; status = (int)r.Status;
                    }
                    catch (Exception ex)
                    {
                        body = ex.GetType().Name; status = -1;
                    }
                    sw.Stop();
                    if (sw.ElapsedMilliseconds > 1500) slow.Add(method + " " + shape + " " + sw.ElapsedMilliseconds + "ms");
                    if (status != 200 || body.Contains("-32603") || body.Contains("Internal error"))
                        offenders.Add(method + " " + shape + " -> " + status + " " + (body.Length > 160 ? body.Substring(0, 160) : body));
                }
            }

            _out.WriteLine("requests: " + total + " offenders: " + offenders.Count + " slow: " + slow.Count);
            foreach (var s in slow) _out.WriteLine("SLOW " + s);
            foreach (var o in offenders) _out.WriteLine("OFFENDER " + o);
            List<(LogLevel Level, string Category, string Message, Exception? Exception)> entries;
            lock (host.Log.Entries) entries = host.Log.Entries.ToList();
            foreach (var g in entries.Where(e => e.Exception != null).GroupBy(e => e.Level + " " + e.Exception!.GetType().Name + " " + e.Message.Split(':')[0]))
                _out.WriteLine("LOG " + g.Count() + "x " + g.Key);
            Assert.Empty(offenders);
        }
    }
}
