using System.Linq;
using Nethereum.CoreChain.RocksDB.History;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class FreezerHistoryIndexCompactionProfileTests
    {
        [Fact]
        public void FreezerHistoryHashIndexCfs_UseUniversalCompaction_FilterMapsUsesLiveIndex_NotBulkFirehose()
        {
            var byName = HistoryColumnFamilies.FreezerHistoryCatalogue.ToDictionary(c => c.Name, c => c.Profile);
            Assert.Equal(HistoryColumnFamilies.HistoryCfProfile.UniversalIndex, byName[HistoryColumnFamilies.TxHashIndex]);
            Assert.Equal(HistoryColumnFamilies.HistoryCfProfile.UniversalIndex, byName[HistoryColumnFamilies.BlockHashIndex]);
            Assert.Equal(HistoryColumnFamilies.HistoryCfProfile.LiveIndex, byName[HistoryColumnFamilies.LogFilterMaps]);
            Assert.Equal(HistoryColumnFamilies.HistoryCfProfile.Control, byName[HistoryColumnFamilies.Control]);
        }

        [Fact]
        public void GenesisFirehoseCatalogue_KeepsTheDeferredBulkProfile_ForTheIndexCfs()
        {
            foreach (var cf in new[] { HistoryColumnFamilies.TxHashIndex, HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.LogFilterMaps })
                Assert.Equal(
                    HistoryColumnFamilies.HistoryCfProfile.Bulk,
                    HistoryColumnFamilies.Catalogue.Single(c => c.Name == cf).Profile);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("not-a-number", true)]
        [InlineData("0", false)]
        [InlineData("1000000000", false)]
        public void ExceedsFinalCompactionThreshold_CompactsOnlyWhenPendingDebtIsHighOrUnreadable(string property, bool expected)
        {
            Assert.Equal(expected, BulkIndexIngestor.ExceedsFinalCompactionThreshold(property));
        }

        [Fact]
        public void ExceedsFinalCompactionThreshold_SkipsAtThreshold_CompactsJustAbove()
        {
            var threshold = BulkIndexIngestor.FinishCompactionPendingThresholdBytes;
            Assert.False(BulkIndexIngestor.ExceedsFinalCompactionThreshold(threshold.ToString()));
            Assert.True(BulkIndexIngestor.ExceedsFinalCompactionThreshold((threshold + 1).ToString()));
        }
    }
}
