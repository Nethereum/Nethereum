using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB;
using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class BackgroundFreezeIndexingDefaultsTests
    {
        [Fact]
        public void Given_DefaultConfigs_Then_BackgroundFreezeIndexingIsOnEverywhere()
        {
            Assert.True(new RocksDbStorageOptions().BackgroundFreezeIndexing);
            Assert.True(new MainnetChainServerConfig().BackgroundFreezeIndexing);
            Assert.True(new ChainNodeStorageConfig().BackgroundFreezeIndexing);
            Assert.True(new MainnetChainServerConfig().ToChainNodeConfig().Storage.BackgroundFreezeIndexing);
        }
    }
}
