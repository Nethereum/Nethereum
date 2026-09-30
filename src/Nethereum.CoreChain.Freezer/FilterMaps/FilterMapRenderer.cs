using System.Collections.Generic;
using Nethereum.Freezer.FilterMaps;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public sealed class FilterMapRenderer
    {
        private readonly FilterMapsParams _p;
        private readonly Dictionary<long, Dictionary<int, List<uint>>> _rowsByMap = new Dictionary<long, Dictionary<int, List<uint>>>();

        public FilterMapRenderer(FilterMapsParams p)
        {
            _p = p;
        }

        public void Mark(long mapIndex, long lvIndex, byte[] valueHash)
        {
            var layer = 0;
            var rowIndex = LogValueHasher.RowIndex(mapIndex, layer, valueHash, _p);
            var row = RowFor(mapIndex, rowIndex);

            while (row.Count >= _p.MaxRowLength(layer))
            {
                layer++;
                rowIndex = LogValueHasher.RowIndex(mapIndex, layer, valueHash, _p);
                row = RowFor(mapIndex, rowIndex);
            }

            row.Add((uint)LogValueHasher.ColumnIndex(lvIndex, valueHash, _p));
        }

        public void AdoptMap(long mapIndex, FilterMapRenderer source)
        {
            if (source._rowsByMap.TryGetValue(mapIndex, out var rowsOfMap))
                _rowsByMap[mapIndex] = rowsOfMap;
        }

        public IEnumerable<long> TouchedMapIndices => _rowsByMap.Keys;

        public IReadOnlyDictionary<int, List<uint>> RowsOfMap(long mapIndex) =>
            _rowsByMap.TryGetValue(mapIndex, out var rows) ? rows : EmptyRows;

        private static readonly IReadOnlyDictionary<int, List<uint>> EmptyRows = new Dictionary<int, List<uint>>();

        private List<uint> RowFor(long mapIndex, int rowIndex)
        {
            if (!_rowsByMap.TryGetValue(mapIndex, out var rowsOfMap))
            {
                rowsOfMap = new Dictionary<int, List<uint>>();
                _rowsByMap[mapIndex] = rowsOfMap;
            }

            if (!rowsOfMap.TryGetValue(rowIndex, out var row))
            {
                row = new List<uint>();
                rowsOfMap[rowIndex] = row;
            }

            return row;
        }
    }
}
