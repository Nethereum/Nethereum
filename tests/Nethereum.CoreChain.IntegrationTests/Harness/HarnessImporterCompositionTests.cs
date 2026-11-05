using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.Node.HarnessServer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Harness
{
    public class HarnessImporterCompositionTests
    {
        [Fact]
        public void Given_ADevChainNode_When_TheHarnessImporterIsComposed_Then_ItAdoptsByDifficultyOverTheBundleMetadata()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDevChainServer(new DevChainServerConfig { Storage = "memory" });
            using var provider = services.BuildServiceProvider();
            var node = provider.GetRequiredService<DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();

            var importer = HarnessNodeHost.CreateImporter(node, bundle);

            Assert.NotNull(importer.SideChain!.ForkChoice);
            Assert.Same(bundle.Metadata, importer.SideChain.Metadata);
        }
    }
}
