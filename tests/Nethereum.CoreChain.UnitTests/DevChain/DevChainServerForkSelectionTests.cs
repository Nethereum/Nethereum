using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainServerForkSelectionTests
    {
        [Fact]
        public void Given_NoHardforkIsStated_When_TheServerConfigIsConverted_Then_ItRunsAmsterdam()
        {
            var resolved = new DevChainServerConfig().GetLiveChainConfig();

            Assert.Equal("amsterdam", resolved.Hardfork);
            Assert.Equal(HardforkName.Amsterdam, resolved.PinnedFork);
        }

        [Fact]
        public void Given_PragueIsStated_When_TheServerConfigIsConverted_Then_TheDevChainConfigCarriesIt()
        {
            var resolved = new DevChainServerConfig { Hardfork = "prague" }.GetLiveChainConfig();

            Assert.Equal("prague", resolved.Hardfork);
            Assert.Equal(HardforkName.Prague, resolved.PinnedFork);
        }

        [Fact]
        public void Given_AStatedHardfork_When_ItDiffersFromTheDefault_Then_TheTwoDoNotResolveAlike()
        {
            var stated = new DevChainServerConfig { Hardfork = "prague" }.GetLiveChainConfig();
            var defaulted = new DevChainServerConfig().GetLiveChainConfig();

            Assert.NotEqual(stated.PinnedFork, defaulted.PinnedFork);
        }

        [Fact]
        public void Given_TheServerDefault_When_ComparedToTheLibraryDefault_Then_TheServerIsAheadOfIt()
        {
            Assert.Equal(HardforkName.Prague, HardforkNames.Parse(ChainConfig.DefaultHardfork));
            Assert.True(HardforkNames.Parse(DevChainServerConfig.DefaultServerHardfork) > HardforkNames.Parse(ChainConfig.DefaultHardfork),
                "the devchain server runs ahead of the library default on purpose; if the library default moves, " +
                "revisit whether the server still needs its own");
        }

        [Fact]
        public void Given_SqliteStorage_When_TheServerIsComposed_Then_TheBlockAccessListStoreIsBackedBySqlite()
        {
            using var provider = new ServiceCollection()
                .AddDevChainServer(new DevChainServerConfig { Storage = "sqlite", Persist = false })
                .BuildServiceProvider();

            Assert.IsType<SqliteBlockAccessListStore>(provider.GetRequiredService<IBlockAccessListStore>());
        }

        [Fact]
        public void Given_MemoryStorage_When_TheServerIsComposed_Then_TheBlockAccessListStoreIsInMemory()
        {
            using var provider = new ServiceCollection()
                .AddDevChainServer(new DevChainServerConfig { Storage = "memory" })
                .BuildServiceProvider();

            Assert.IsType<InMemoryBlockAccessListStore>(provider.GetRequiredService<IBlockAccessListStore>());
        }

        [Fact]
        public void Given_SqliteStorage_When_TheNodeIsResolved_Then_ItUsesTheRegisteredStoreRatherThanItsOwnSubstitute()
        {
            using var provider = new ServiceCollection()
                .AddDevChainServer(new DevChainServerConfig { Storage = "sqlite", Persist = false })
                .BuildServiceProvider();

            var registered = provider.GetRequiredService<IBlockAccessListStore>();
            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();

            Assert.Same(registered, node.BlockAccessLists);
        }

        [Fact]
        public void Given_TheComposedNode_When_ItIsResolved_Then_ItUsesTheRegisteredStoreRatherThanItsOwnSubstitute()
        {
            using var provider = new ServiceCollection()
                .AddDevChainServer(new DevChainServerConfig { Storage = "memory" })
                .BuildServiceProvider();

            var registered = provider.GetRequiredService<IBlockAccessListStore>();
            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();

            Assert.Same(registered, node.BlockAccessLists);
        }
    }
}
