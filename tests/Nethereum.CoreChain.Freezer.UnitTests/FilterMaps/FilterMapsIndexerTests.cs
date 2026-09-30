using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsIndexerTests
    {
        private static readonly FilterMapsParams P = FilterMapsTestSupport.TestParams;

        private static FakeChainView OneCleanEpochChain()
        {
            var chain = new FakeChainView();
            chain.SetBlock(0,
                FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 1, 2, 3 }),
                FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 4, 5, 6 }));
            chain.SetBlock(1,
                FilterMapsTestSupport.MakeLog(addressSeed: 3, topicSeeds: new byte[] { 7, 8, 9, 10, 11, 12 }));
            return chain;
        }

        [Fact]
        public void Given_RenderedEpoch_When_ReadRange_Then_BlocksAndMapsAfterLastAdvanced()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = OneCleanEpochChain();
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.Equal(-1, indexer.IndexedHeadBlock);

            var rendered = indexer.RenderHead();

            Assert.True(rendered);
            var range = store.ReadRange();
            Assert.NotNull(range);
            Assert.Equal(0, range.BlocksFirst);
            Assert.Equal(2, range.BlocksAfterLast);
            Assert.Equal(0, range.MapsFirst);
            Assert.Equal(2, range.MapsAfterLast);
            Assert.Equal(16, range.HeadDelimiter);
            Assert.Equal(1, indexer.IndexedHeadBlock);

            Assert.False(indexer.RenderHead());
            Assert.Equal(range.MapsAfterLast, store.ReadRange().MapsAfterLast);
        }

        [Fact]
        public void Given_RenderThenRollbackTo_When_ReadRange_Then_RolledToWholeEpoch()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = OneCleanEpochChain();
            chain.SetBlock(2,
                FilterMapsTestSupport.MakeLog(addressSeed: 4, topicSeeds: new byte[] { 13, 14, 15, 16, 17, 18 }));
            chain.SetBlock(3,
                FilterMapsTestSupport.MakeLog(addressSeed: 5, topicSeeds: new byte[] { 19, 20, 21, 22, 23, 24 }));

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 3 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());
            Assert.True(indexer.RenderHead());
            Assert.Equal(3, indexer.IndexedHeadBlock);
            Assert.Equal(4, store.ReadRange().MapsAfterLast);

            var rolledBack = indexer.RollbackTo(1);

            Assert.True(rolledBack);
            var range = store.ReadRange();
            Assert.Equal(2, range.BlocksAfterLast);
            Assert.Equal(2, range.MapsAfterLast);
            Assert.Equal(1, indexer.IndexedHeadBlock);

            Assert.Null(store.ReadBaseRowGroup(FilterMapsSchema.MapRowIndex(2, 0, P)));
        }

        [Fact]
        public void Given_CorpusReceipts_When_RenderThenQueryARenderedRow_Then_ContainsExpectedColumn()
        {
            var chain = new CorpusChainView();
            var store = new InMemoryFilterMapsStore();
            var finality = new FakeFinalitySource { FinalizedBlockNumber = chain.HeadNumber };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());

            var rangeAfterRender = store.ReadRange();
            LogValueEntry? target = null;
            foreach (var entry in LogValueSequence.Enumerate(0, rangeAfterRender.BlocksAfterLast - 1, 0, null, chain, P))
            {
                if (entry.IsBlockDelimiter || entry.LvIndex >= rangeAfterRender.HeadDelimiter)
                    continue;
                target = entry;
                break;
            }

            Assert.True(target.HasValue, "expected at least one log value within the committed range");
            var t = target.Value;

            var mapIndex = t.LvIndex >> P.LogValuesPerMap;
            var expectedColumn = (uint)LogValueHasher.ColumnIndex(t.LvIndex, t.ValueHash, P);

            var layer = 0;
            var found = false;
            while (!found)
            {
                var rowIndex = LogValueHasher.RowIndex(mapIndex, layer, t.ValueHash, P);
                var groupMapRowIndex = FilterMapsSchema.MapRowIndex(mapIndex - (mapIndex % P.BaseRowGroupSize), rowIndex, P);
                var groupBytes = store.ReadBaseRowGroup(groupMapRowIndex);
                var groupLength = (int)System.Math.Min(P.BaseRowGroupSize, rangeAfterRender.MapsAfterLast - (mapIndex - (mapIndex % P.BaseRowGroupSize)));
                var rows = groupBytes == null
                    ? Enumerable.Repeat(FilterRow.Empty, groupLength).ToList()
                    : FilterMapsRowCodec.DecodeBaseRowGroup(groupBytes, groupLength, P).ToList();
                var baseRow = rows[(int)(mapIndex % P.BaseRowGroupSize)];

                var extBytes = store.ReadExtRow(FilterMapsSchema.MapRowIndex(mapIndex, rowIndex, P));
                var extRow = extBytes == null ? FilterRow.Empty : FilterMapsRowCodec.DecodeExtRow(extBytes, P);

                var fullRow = baseRow.Columns.Concat(extRow.Columns).ToList();
                if (fullRow.Contains(expectedColumn))
                {
                    found = true;
                    break;
                }

                var maxLen = P.MaxRowLength(layer);
                if (fullRow.Count < maxLen)
                    break;

                layer++;
            }

            Assert.True(found,
                $"expected column {expectedColumn} (lvIndex {t.LvIndex}, map {mapIndex}) not found in the rendered row -- render/hash mismatch");
        }

        [Fact]
        public void Given_StraddlingEpochBoundary_When_RenderedAcrossTwoPasses_Then_NoMarksLostOrOverwritten()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = new FakeChainView();

            var logA = FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 2, 3 });
            chain.SetBlock(0, logA);

            var logB = FilterMapsTestSupport.MakeLog(addressSeed: 4, topicSeeds: new byte[] { 5, 6 });
            var logC = FilterMapsTestSupport.MakeLog(addressSeed: 7);
            var logD = FilterMapsTestSupport.MakeLog(addressSeed: 8, topicSeeds: new byte[] { 9, 10, 11, 12, 13, 14, 15 });
            var logE = FilterMapsTestSupport.MakeLog(addressSeed: 16, topicSeeds: new byte[] { 17, 18, 19 });
            chain.SetBlock(1, logB, logC, logD, logE);

            var logF = FilterMapsTestSupport.MakeLog(addressSeed: 20, topicSeeds: new byte[] { 21, 22 });
            var logG = FilterMapsTestSupport.MakeLog(addressSeed: 23, topicSeeds: new byte[] { 24, 25, 26, 27, 13, 29, 30 });
            chain.SetBlock(2, logF, logG);

            var logH = FilterMapsTestSupport.MakeLog(addressSeed: 31, topicSeeds: new byte[] { 32, 33, 34, 35, 36, 37 });
            chain.SetBlock(3, logH);

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());
            Assert.Equal(0, indexer.IndexedHeadBlock);

            finality.FinalizedBlockNumber = 3;
            Assert.True(indexer.RenderHead());

            Assert.Equal(2, indexer.IndexedHeadBlock);
            var range = store.ReadRange();
            Assert.Equal(3, range.BlocksAfterLast);
            Assert.Equal(4, range.MapsAfterLast);

            foreach (var entry in LogValueSequence.Enumerate(0, 2, 0, null, chain, P))
            {
                if (entry.IsBlockDelimiter)
                    continue;

                AssertMarkStored(store, entry.LvIndex, entry.ValueHash, range.MapsAfterLast);
            }

            var eHash = LogValueHasher.AddressValue(FilterMapsTestSupport.Address((byte)16).HexToByteArray());
            AssertMarkStored(store, lvIndex: 16, eHash, range.MapsAfterLast);
            var gHash = LogValueHasher.TopicValue(FilterMapsTestSupport.Topic(13));
            AssertMarkStored(store, lvIndex: 29, gHash, range.MapsAfterLast);
        }

        [Fact]
        public void Given_RenderOvershootsEpochViaSkip_When_RollbackToEarlierBlock_Then_SurvivingMapDataIntact()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = new FakeChainView();

            var log0 = FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 2, 3, 4, 5, 6, 7, 8 });
            chain.SetBlock(0, log0);

            var log1 = FilterMapsTestSupport.MakeLog(addressSeed: 9, topicSeeds: new byte[] { 10, 11, 12, 13, 14, 15, 16 });
            chain.SetBlock(1, log1);

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());

            var rangeAfterRender = store.ReadRange();
            Assert.Equal(0, rangeAfterRender.MapsAfterLast % P.MapsPerEpoch);

            indexer.RollbackTo(0);

            var range = store.ReadRange();
            Assert.Equal(0, indexer.IndexedHeadBlock);

            foreach (var entry in LogValueSequence.Enumerate(0, 0, 0, null, chain, P))
            {
                if (entry.IsBlockDelimiter)
                    continue;

                AssertMarkStored(store, entry.LvIndex, entry.ValueHash, range.MapsAfterLast);
            }
        }

        [Fact]
        public void Given_RollbackTo_When_MapHadExtRow_Then_ExtRowAlsoDeleted()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = new FakeChainView();

            chain.SetBlock(0,
                FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 1, 2, 3 }),
                FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 4, 5, 6 }));
            chain.SetBlock(1,
                FilterMapsTestSupport.MakeLog(addressSeed: 3, topicSeeds: new byte[] { 7, 8, 9, 10, 11, 12 }));

            var colliding = FilterMapsTestSupport.FindColumnCollidingAddressSeeds(mapIndex: 2, count: 5, P);
            var block2Logs = new List<Log>();
            foreach (var seed in colliding)
                block2Logs.Add(new Log { Address = FilterMapsTestSupport.Address(seed), Data = null, Topics = new List<byte[]>() });
            block2Logs.Add(FilterMapsTestSupport.MakeLog(addressSeed: 250, topicSeeds: new byte[] { 251 }));
            chain.SetBlock(2, block2Logs.ToArray());
            chain.SetBlock(3, FilterMapsTestSupport.MakeLog(addressSeed: 5, topicSeeds: new byte[] { 19, 20, 21, 22, 23, 24 }));

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 3 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());
            Assert.True(indexer.RenderHead());

            var probeHash = LogValueHasher.AddressValue(FilterMapsTestSupport.Address(colliding[4]).HexToByteArray());
            var extRowIndex = LogValueHasher.RowIndex(mapIndex: 2, layer: 1, probeHash, P);
            var extKey = FilterMapsSchema.MapRowIndex(2, extRowIndex, P);
            Assert.NotNull(store.ReadExtRow(extKey));

            var rolledBack = indexer.RollbackTo(1);

            Assert.True(rolledBack);
            Assert.Null(store.ReadBaseRowGroup(FilterMapsSchema.MapRowIndex(2, 0, P)));
            Assert.Null(store.ReadExtRow(extKey));
        }

        [Fact]
        public void Given_RowExceedsBaseRowLength_When_Rendered_Then_BaseAndExtRowsBothStored()
        {
            var store = new InMemoryFilterMapsStore();
            var chain = new FakeChainView();

            var colliding = FilterMapsTestSupport.FindColumnCollidingAddressSeeds(mapIndex: 0, count: 5, P);
            var block0Logs = new List<Log>();
            foreach (var seed in colliding)
                block0Logs.Add(new Log { Address = FilterMapsTestSupport.Address(seed), Data = null, Topics = new List<byte[]>() });
            block0Logs.Add(FilterMapsTestSupport.MakeLog(addressSeed: 250, topicSeeds: new byte[] { 251, 252 }));
            chain.SetBlock(0, block0Logs.ToArray());
            chain.SetBlock(1, FilterMapsTestSupport.MakeLog(addressSeed: 253, topicSeeds: new byte[] { 1, 2, 3, 4, 5, 6 }));

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            Assert.True(indexer.RenderHead());
            var range = store.ReadRange();

            var probeHash = LogValueHasher.AddressValue(FilterMapsTestSupport.Address(colliding[4]).HexToByteArray());
            var rowIndex = LogValueHasher.RowIndex(mapIndex: 0, layer: 1, probeHash, P);

            var groupMapRowIndex = FilterMapsSchema.MapRowIndex(0, rowIndex, P);
            var baseBytes = store.ReadBaseRowGroup(groupMapRowIndex);
            Assert.NotNull(baseBytes);
            var baseRows = FilterMapsRowCodec.DecodeBaseRowGroup(baseBytes, P.BaseRowGroupSize, P);
            Assert.Equal(P.BaseRowLength, baseRows[0].Columns.Count);

            var extBytes = store.ReadExtRow(FilterMapsSchema.MapRowIndex(0, rowIndex, P));
            Assert.NotNull(extBytes);
            var extRow = FilterMapsRowCodec.DecodeExtRow(extBytes, P);
            Assert.True(extRow.Columns.Count >= 1);

            for (var i = 0; i < colliding.Count; i++)
            {
                var hash = LogValueHasher.AddressValue(FilterMapsTestSupport.Address(colliding[i]).HexToByteArray());
                AssertMarkStored(store, lvIndex: i, hash, range.MapsAfterLast);
            }
        }

        private static void AssertMarkStored(IFilterMapsStore store, long lvIndex, byte[] valueHash, long mapsAfterLast)
        {
            var mapIndex = lvIndex >> P.LogValuesPerMap;
            var expectedColumn = (uint)LogValueHasher.ColumnIndex(lvIndex, valueHash, P);

            var layer = 0;
            var found = false;
            while (!found)
            {
                var rowIndex = LogValueHasher.RowIndex(mapIndex, layer, valueHash, P);
                var groupStart = mapIndex - (mapIndex % P.BaseRowGroupSize);
                var groupMapRowIndex = FilterMapsSchema.MapRowIndex(groupStart, rowIndex, P);
                var groupBytes = store.ReadBaseRowGroup(groupMapRowIndex);
                var groupLength = (int)System.Math.Min(P.BaseRowGroupSize, mapsAfterLast - groupStart);
                var rows = groupBytes == null
                    ? Enumerable.Repeat(FilterRow.Empty, groupLength).ToList()
                    : FilterMapsRowCodec.DecodeBaseRowGroup(groupBytes, groupLength, P).ToList();
                var baseRow = rows[(int)(mapIndex - groupStart)];

                var extBytes = store.ReadExtRow(FilterMapsSchema.MapRowIndex(mapIndex, rowIndex, P));
                var extRow = extBytes == null ? FilterRow.Empty : FilterMapsRowCodec.DecodeExtRow(extBytes, P);

                var fullRow = baseRow.Columns.Concat(extRow.Columns).ToList();
                if (fullRow.Contains(expectedColumn))
                {
                    found = true;
                    break;
                }

                var maxLen = P.MaxRowLength(layer);
                if (fullRow.Count < maxLen)
                    break;

                layer++;
            }

            Assert.True(found,
                $"expected column {expectedColumn} (lvIndex {lvIndex}, map {mapIndex}) not found -- mark dropped or overwritten");
        }
    }
}
