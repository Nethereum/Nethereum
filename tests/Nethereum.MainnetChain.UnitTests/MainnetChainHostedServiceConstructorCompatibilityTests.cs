using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainHostedServiceConstructorCompatibilityTests
    {
        private sealed class EmptyBlockSource : IBlockSource
        {
            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(default(BlockSourceHealth));

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal? LastChainBreak => null;
        }

        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.UseInMemoryBundleAndSource(new EmptyBlockSource());
            services.AddMainnetChainServer(new MainnetChainServerConfig { DataDir = null });
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Given_TheShippedFifteenParameterConstructor_When_TheServiceIsBuilt_Then_ItIsConstructedWithTheSuppliedDependencies()
        {
            using var provider = BuildProvider();
            var mapped = new MainnetChainServerConfig { DataDir = null }.ToChainNodeConfig();

#pragma warning disable CS0618
            var service = new MainnetChainHostedService(
                provider.GetRequiredService<MainnetChainNodeFactory>(),
                provider.GetRequiredService<IChainStoreBundle>(),
                provider.GetRequiredService<IBlockSource>(),
                provider.GetRequiredService<IValidationPolicy>(),
                provider.GetRequiredService<MainnetChainServerConfig>(),
                provider.GetRequiredService<ILogger<MainnetChainHostedService>>(),
                provider.GetRequiredService<MainnetChainNodeAccessor>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                mapped);
#pragma warning restore CS0618

            Assert.Null(service.LastResult);
        }

        [Fact]
        public void Given_TheDefaultServiceRegistration_When_HostedServicesAreResolved_Then_TheMainnetChainHostedServiceIsConstructedThroughDependencyInjection()
        {
            using var provider = BuildProvider();

            var hosted = provider.GetServices<IHostedService>();

            Assert.Contains(hosted, h => h is MainnetChainHostedService);
        }
    }
}
