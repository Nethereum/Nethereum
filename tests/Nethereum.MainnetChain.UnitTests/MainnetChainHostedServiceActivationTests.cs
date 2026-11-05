using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetChainHostedServiceActivationTests
    {
        private sealed class HostLifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;
            public void StopApplication() { }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("http://127.0.0.1:1")]
        public async Task Given_TheServerCompositionWithTheTipBandAndTheHostLifetime_When_TheHostedServicesAreResolved_Then_EveryHostedServiceResolvesIncludingTheMainnetChainHostedService(string? beaconEndpoint)
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-hosted-activation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);

            try
            {
                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    ListenPort = -1,
                    DisableDiscv4 = true,
                    DisableDiscv5 = true,
                    TargetPeers = 1,
                    LightClient = beaconEndpoint == null ? null : new LightClientConfigSection { BeaconEndpoint = beaconEndpoint },
                };

                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton<IHostApplicationLifetime>(new HostLifetime());
                services.AddMainnetChainServer(config);
                await services.AddMainnetNodeAsync(config, NullLoggerFactory.Instance);

                await using var provider = services.BuildServiceProvider();

                var hosted = provider.GetServices<IHostedService>().ToList();

                Assert.Contains(hosted, h => h is MainnetChainHostedService);
            }
            finally
            {
                try { Directory.Delete(dataDir, recursive: true); }
                catch { }
            }
        }
    }
}
