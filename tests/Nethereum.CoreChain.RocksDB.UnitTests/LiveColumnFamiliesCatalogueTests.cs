using System.Linq;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class LiveColumnFamiliesCatalogueTests
    {
        [Fact]
        public void Catalogue_Covers_Every_Live_ColumnFamily_Exactly_Once()
        {
            var names = LiveColumnFamilies.Catalogue.Select(x => x.Name).ToArray();

            Assert.Equal(names.Length, names.Distinct().Count());
        }

        [Fact]
        public void Manager_OpenSet_Matches_Catalogue()
        {
            var catalogue = LiveColumnFamilies.Catalogue.Select(x => x.Name).OrderBy(x => x).ToArray();
            var openSet = RocksDbManager.ColumnFamilyNames.OrderBy(x => x).ToArray();

            Assert.Equal(catalogue, openSet);
        }

        [Theory]
        [InlineData("state_accounts", LiveCfProfile.RandomPoint)]
        [InlineData("trie_nodes", LiveCfProfile.RandomPoint)]
        [InlineData("state_storage", LiveCfProfile.RandomPoint)]
        [InlineData("blocks", LiveCfProfile.LocationSequential)]
        [InlineData("receipts", LiveCfProfile.LocationSequential)]
        [InlineData("metadata", LiveCfProfile.HotTiny)]
        [InlineData("state_history_meta", LiveCfProfile.HotTiny)]
        [InlineData("block_numbers", LiveCfProfile.SecondaryIndex)]
        [InlineData("log_by_address", LiveCfProfile.SecondaryIndex)]
        public void Assigns_Expected_Profile(string cf, LiveCfProfile expected)
            => Assert.Equal(expected, LiveColumnFamilies.Catalogue.Single(x => x.Name == cf).Profile);
    }
}
