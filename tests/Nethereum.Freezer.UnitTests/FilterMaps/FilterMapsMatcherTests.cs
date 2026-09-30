using System.Collections.Generic;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsMatcherTests
    {
        private static readonly FilterMapsParams P = new FilterMapsParams(
            logMapHeight: 4, logMapWidth: 8, logMapsPerEpoch: 1, logValuesPerMap: 3,
            baseRowGroupSize: 2, baseRowLengthRatio: 4, logLayerDiff: 4);

        private static byte[] Value(byte seed)
        {
            var bytes = new byte[32];
            bytes[0] = seed;
            return bytes;
        }

        private static FilterMapsQuery SinglePosition(params byte[][] alternatives) =>
            new FilterMapsQuery(new List<IReadOnlyList<byte[]>> { alternatives });

        [Fact]
        public void Given_SingleAddress_When_GetPotentialMatches_Then_ReturnsLvWhereAddressMarked()
        {
            var backend = new FakeQueryBackend(P);
            backend.SetBlockLvPointer(0, 0);
            backend.SetBlockLvPointer(1, 8);

            var address = Value(1);
            backend.Mark(mapIndex: 0, lvIndex: 3, address);

            var matcher = new FilterMapsMatcher(backend);
            var matches = matcher.GetPotentialMatches(SinglePosition(address), fromBlock: 0, toBlock: 0);

            Assert.Equal(new long[] { 3 }, matches);
        }

        [Fact]
        public void Given_AddressAndTopic_When_Sequence_Then_RequiresConsecutiveLvHits()
        {
            var backend = new FakeQueryBackend(P);
            backend.SetBlockLvPointer(0, 0);
            backend.SetBlockLvPointer(1, 8);

            var address = Value(1);
            var topic = Value(2);

            backend.Mark(mapIndex: 0, lvIndex: 2, address);
            backend.Mark(mapIndex: 0, lvIndex: 3, topic);

            backend.Mark(mapIndex: 0, lvIndex: 5, address);
            backend.Mark(mapIndex: 0, lvIndex: 0, topic);

            var query = new FilterMapsQuery(new List<IReadOnlyList<byte[]>>
            {
                new[] { address },
                new[] { topic },
            });

            var matcher = new FilterMapsMatcher(backend);
            var matches = matcher.GetPotentialMatches(query, fromBlock: 0, toBlock: 0);

            Assert.Equal(new long[] { 2 }, matches);
        }

        [Fact]
        public void Given_RowFullEscalatesLayer_When_Match_Then_UsesHigherLayer()
        {
            var backend = new FakeQueryBackend(P);
            backend.SetBlockLvPointer(0, 0);
            backend.SetBlockLvPointer(1, 8);

            var target = Value(1);
            var layer0Row = LogValueHasher.RowIndex(mapIndex: 0, layer: 0, target, P);

            const uint filler1 = 200;
            const uint filler2 = 201;
            Assert.NotEqual(filler1, (uint)LogValueHasher.ColumnIndex(6, target, P));
            Assert.NotEqual(filler2, (uint)LogValueHasher.ColumnIndex(6, target, P));
            backend.PutColumn(mapIndex: 0, layer0Row, filler1);
            backend.PutColumn(mapIndex: 0, layer0Row, filler2);

            backend.Mark(mapIndex: 0, lvIndex: 4, target);

            var matcher = new FilterMapsMatcher(backend);
            var matches = matcher.GetPotentialMatches(SinglePosition(target), fromBlock: 0, toBlock: 0);

            Assert.Equal(new long[] { 4 }, matches);
        }

        [Fact]
        public void Given_AccidentalRowCollision_When_ColumnMismatch_Then_Filtered()
        {
            var backend = new FakeQueryBackend(P);
            backend.SetBlockLvPointer(0, 0);
            backend.SetBlockLvPointer(1, 8);

            var target = Value(1);
            const long collidingLv = 6;
            var targetColumnAtCollidingLv = (uint)LogValueHasher.ColumnIndex(collidingLv, target, P);

            uint bogusColumn = 0;
            var found = false;
            for (var otherSeed = 2; otherSeed <= 255; otherSeed++)
            {
                bogusColumn = (uint)LogValueHasher.ColumnIndex(collidingLv, Value((byte)otherSeed), P);
                if (bogusColumn == targetColumnAtCollidingLv) continue;
                found = true;
                break;
            }
            Assert.True(found, "expected at least one seed in [2,255] whose column at lv6 differs from target's");

            var targetRow0 = LogValueHasher.RowIndex(mapIndex: 0, layer: 0, target, P);
            backend.PutColumn(mapIndex: 0, targetRow0, bogusColumn);

            var matcher = new FilterMapsMatcher(backend);
            var matches = matcher.GetPotentialMatches(SinglePosition(target), fromBlock: 0, toBlock: 0);

            Assert.Empty(matches);
        }

        private sealed class FakeQueryBackend : IFilterMapsQueryBackend
        {
            private readonly Dictionary<long, long> _blockLvPointers = new Dictionary<long, long>();
            private readonly Dictionary<(long mapIndex, int rowIndex), List<uint>> _rows = new Dictionary<(long, int), List<uint>>();

            public FilterMapsParams Params { get; }

            public FakeQueryBackend(FilterMapsParams p) => Params = p;

            public void SetBlockLvPointer(long blockNumber, long lvPointer) => _blockLvPointers[blockNumber] = lvPointer;

            public void PutColumn(long mapIndex, int rowIndex, uint column) => RowFor(mapIndex, rowIndex).Add(column);

            public void Mark(long mapIndex, long lvIndex, byte[] value)
            {
                var layer = 0;
                var rowIndex = LogValueHasher.RowIndex(mapIndex, layer, value, Params);
                var row = RowFor(mapIndex, rowIndex);
                while (row.Count >= Params.MaxRowLength(layer))
                {
                    layer++;
                    rowIndex = LogValueHasher.RowIndex(mapIndex, layer, value, Params);
                    row = RowFor(mapIndex, rowIndex);
                }
                row.Add((uint)LogValueHasher.ColumnIndex(lvIndex, value, Params));
            }

            public long GetBlockLvPointer(long blockNumber) => _blockLvPointers[blockNumber];

            public FilterRow GetFilterMapRow(long mapIndex, int rowIndex) =>
                _rows.TryGetValue((mapIndex, rowIndex), out var row) ? new FilterRow(row) : FilterRow.Empty;

            private List<uint> RowFor(long mapIndex, int rowIndex)
            {
                var key = (mapIndex, rowIndex);
                if (!_rows.TryGetValue(key, out var row))
                {
                    row = new List<uint>();
                    _rows[key] = row;
                }
                return row;
            }
        }
    }
}
