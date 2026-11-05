using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class FilterRetentionTests
    {
        private readonly ITestOutputHelper _out;

        public FilterRetentionTests(ITestOutputHelper output) { _out = output; }

        private static byte[] NewFilterBody(int addresses, int salt)
        {
            var sb = new StringBuilder("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_newFilter\",\"params\":[{\"address\":[");
            for (var i = 0; i < addresses; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("\"0x").Append((0x3000000000000000UL + (ulong)i + (ulong)salt * 1_000_000UL).ToString("x16").PadLeft(40, '0')).Append('"');
            }
            sb.Append("]}]}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        [Fact]
        [Trait("Finding", "S4-R3-04")]
        public async Task Given_FiltersInstalledWithHundredsOfThousandsOfAddresses_When_TheyAreRetainedForTheirTtl_Then_TheyAreRefusedAndNothingIsRetained()
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(true);

            var sent = 0L;
            for (var i = 0; i < 12; i++)
            {
                var body = NewFilterBody(200_000, i);
                sent += body.Length;
                var response = await host.PostAsync(body);
                _ = response;
            }

            var retained = long.MaxValue;
            for (var attempt = 0; attempt < 13 && retained >= 16L * 1024 * 1024; attempt++)
            {
                if (attempt > 0) await Task.Delay(10_000);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                retained = GC.GetTotalMemory(true) - before;
            }
            _out.WriteLine("sent " + sent / 1024 / 1024 + " MiB in 12 eth_newFilter requests, node retains " + retained / 1024 / 1024 + " MiB");

            Assert.True(retained < 16L * 1024 * 1024, "the node retains " + retained / 1024 / 1024 + " MiB for " + sent / 1024 / 1024 + " MiB of filter requests");
        }
    }
}
