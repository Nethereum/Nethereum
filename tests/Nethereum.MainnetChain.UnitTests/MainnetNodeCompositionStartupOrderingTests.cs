using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetNodeCompositionStartupOrderingTests
    {
        [Fact]
        public async Task Given_TheMainnetNodeIsComposed_When_APeerAndBlockSourceAreResolvedBeforeTheHostedServiceStarts_Then_TheyAreAlreadyLiveNotThrowing()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-node-startup-ordering-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);

            try
            {
                var services = new ServiceCollection();
                services.AddLogging();

                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    ListenPort = -1,
                    DisableDiscv4 = true,
                    DisableDiscv5 = true,
                    TargetPeers = 1,
                    EnableTxSubmission = true,
                };

                await services.AddMainnetNodeAsync(config, NullLoggerFactory.Instance);

                await using var provider = services.BuildServiceProvider();

                var pool = provider.GetRequiredService<IPeerPool>();
                var scheduler = provider.GetRequiredService<IFetchRequestScheduler>();
                var blockSource = provider.GetRequiredService<IBlockSource>();
                var txSubmission = provider.GetRequiredService<ITransactionSubmissionService>();

                Assert.NotNull(pool);
                Assert.NotNull(scheduler);
                Assert.NotNull(blockSource);
                Assert.NotNull(txSubmission);
            }
            finally
            {
                try { Directory.Delete(dataDir, recursive: true); }
                catch { }
            }
        }
    }
}
