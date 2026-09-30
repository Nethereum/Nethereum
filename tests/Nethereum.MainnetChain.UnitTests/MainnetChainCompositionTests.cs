using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Sync;
using Nethereum.Documentation;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainCompositionTests
    {
        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Compose the follower with no light client resolves the always-accept gate")]
        public void NoLightClient_ResolvesAlwaysAcceptGate()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMainnetChainServer(new MainnetChainServerConfig { DataDir = null });

            using var provider = services.BuildServiceProvider();
            var gate = provider.GetRequiredService<IConsensusBlockGate>();

            Assert.IsType<AlwaysAcceptConsensusBlockGate>(gate);
        }
    }
}
