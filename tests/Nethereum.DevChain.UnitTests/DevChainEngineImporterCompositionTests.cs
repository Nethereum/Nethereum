using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainEngineImporterCompositionTests
    {
        [Fact]
        public void Given_ADevChainNode_When_TheEngineImporterIsComposed_Then_ItCarriesTheDefaultSideChainOverTheBundleMetadata()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDevChainServer(new DevChainServerConfig { Storage = "memory" });
            using var provider = services.BuildServiceProvider();
            var node = provider.GetRequiredService<DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();

            var importer = ServiceCollectionExtensions.CreateEngineImporter(node, bundle);

            Assert.NotNull(importer.SideChain);
            Assert.Null(importer.SideChain!.ForkChoice);
            Assert.Same(bundle.Metadata, importer.SideChain.Metadata);
            Assert.True(importer.SideChain.AcknowledgeKnownCanonicalBlocks);
        }
    }
}
