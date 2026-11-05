using System.Text;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round2
{
    public class AppChainMethodNameRetentionTests
    {
        [Fact]
        [Trait("Finding", "S4-R2-04")]
        public async Task Given_ADefaultAppChain_When_StrangersSendRequestsWithDistinctOneMegabyteMethodNames_Then_TheNodeDoesNotRetainThem()
        {
            await using var host = await AppChainRpcHost.StartAsync();
            var padding = new string('m', 1_000_000);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(true);

            for (var i = 0; i < 40; i++)
            {
                var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"x" + i.ToString("D4") + padding + "\",\"params\":[]}";
                await host.PostAsync(body);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var retained = GC.GetTotalMemory(true) - before;

            Assert.True(retained < 24L * 1024 * 1024,
                $"40 requests with 1,000,000 character method names left {retained / 1024 / 1024} MiB retained");
        }
    }
}
