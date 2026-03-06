using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nethereum.AppChain.Server.Hosting;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class HealthCheckTests
    {
        [Fact]
        public async Task SequencerHealthCheck_NullSequencer_ReturnsHealthy()
        {
            var check = new SequencerHealthCheck(null);
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("Follower", result.Description);
        }

        [Fact]
        public async Task SyncHealthCheck_NullSync_ReturnsHealthy()
        {
            var check = new SyncHealthCheck(null);
            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("No sync", result.Description);
        }
    }
}
