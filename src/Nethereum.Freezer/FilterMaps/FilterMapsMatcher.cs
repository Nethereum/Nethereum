using System;
using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public sealed class FilterMapsMatcher
    {
        private const int MaxLayerSearch = 64;

        private readonly IFilterMapsQueryBackend _backend;

        public FilterMapsMatcher(IFilterMapsQueryBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public IReadOnlyList<long> GetPotentialMatches(FilterMapsQuery query, long fromBlock, long toBlock)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (toBlock < fromBlock) throw new ArgumentException("toBlock must be >= fromBlock", nameof(toBlock));

            var p = _backend.Params;
            var firstIndex = _backend.GetBlockLvPointer(fromBlock);
            var afterLastIndex = _backend.GetBlockLvPointer(toBlock + 1);
            var lastIndex = afterLastIndex > 0 ? afterLastIndex - 1 : 0;

            var firstMap = firstIndex >> p.LogValuesPerMap;
            var lastMap = lastIndex >> p.LogValuesPerMap;

            var results = new List<long>();
            for (var mapIndex = firstMap; mapIndex <= lastMap; mapIndex++)
            {
                var matches = MatchSequence(query, mapIndex, p);
                if (matches == null)
                {
                    var mapFirst = mapIndex << p.LogValuesPerMap;
                    for (var lv = mapFirst; lv < mapFirst + p.ValuesPerMap; lv++)
                        results.Add(lv);
                }
                else
                {
                    results.AddRange(matches);
                }
            }

            var clipped = new List<long>(results.Count);
            foreach (var lv in results)
                if (lv >= firstIndex && lv <= lastIndex)
                    clipped.Add(lv);
            return clipped;
        }

        private List<long> MatchSequence(FilterMapsQuery query, long mapIndex, FilterMapsParams p)
        {
            List<long> accumulated = null;
            var accumulatedIsSet = false;

            for (var position = 0; position < query.Positions.Count; position++)
            {
                var positionMatches = MatchAny(query.Positions[position], mapIndex, p);
                accumulated = accumulatedIsSet
                    ? CombineSequence(accumulated, positionMatches, position, mapIndex, p)
                    : positionMatches;
                accumulatedIsSet = true;
            }

            return accumulated;
        }

        private List<long> MatchAny(IReadOnlyList<byte[]> alternatives, long mapIndex, FilterMapsParams p)
        {
            if (alternatives == null || alternatives.Count == 0)
                return null;

            if (alternatives.Count == 1)
                return SingleValueMatches(alternatives[0], mapIndex, p);

            var merged = new SortedSet<long>();
            foreach (var value in alternatives)
                merged.UnionWith(SingleValueMatches(value, mapIndex, p));
            return new List<long>(merged);
        }

        private static List<long> CombineSequence(List<long> baseRes, List<long> nextRes, long offset, long mapIndex, FilterMapsParams p)
        {
            if (nextRes == null || (baseRes != null && baseRes.Count == 0))
                return baseRes;

            if (baseRes == null || nextRes.Count == 0)
            {
                var mapFirst = mapIndex << p.LogValuesPerMap;
                var min = mapFirst + offset;
                var shifted = new List<long>();
                foreach (var v in nextRes)
                    if (v >= min)
                        shifted.Add(v - offset);
                return shifted;
            }

            var merged = new List<long>();
            var bi = 0;
            var ni = 0;
            while (bi < baseRes.Count && ni < nextRes.Count)
            {
                var b = baseRes[bi];
                var n = nextRes[ni];
                if (n > b + offset) bi++;
                else if (n < b + offset) ni++;
                else { merged.Add(b); bi++; ni++; }
            }
            return merged;
        }

        private List<long> SingleValueMatches(byte[] value, long mapIndex, FilterMapsParams p)
        {
            var accumulatedRows = new List<FilterRow>();

            for (var layer = 0; layer <= MaxLayerSearch; layer++)
            {
                var rowIndex = LogValueHasher.RowIndex(mapIndex, layer, value, p);
                var row = _backend.GetFilterMapRow(mapIndex, rowIndex);
                accumulatedRows.Add(row);

                if (row.Columns.Count < p.MaxRowLength(layer))
                    return PotentialMatches(accumulatedRows, mapIndex, value, p);
            }

            throw new InvalidOperationException(
                $"filter map row for value never stopped being full after {MaxLayerSearch} layers -- corrupt index");
        }

        private static List<long> PotentialMatches(IReadOnlyList<FilterRow> rows, long mapIndex, byte[] value, FilterMapsParams p)
        {
            var mapFirst = mapIndex << p.LogValuesPerMap;
            var hashBits = p.LogMapWidth - p.LogValuesPerMap;

            var results = new List<long>();
            for (var layer = 0; layer < rows.Count; layer++)
            {
                var columns = rows[layer].Columns;
                var maxLen = p.MaxRowLength(layer);
                var rowLen = Math.Min(columns.Count, maxLen);

                for (var i = 0; i < rowLen; i++)
                {
                    var candidateLv = mapFirst + (columns[i] >> hashBits);
                    if (columns[i] == (uint)LogValueHasher.ColumnIndex(candidateLv, value, p))
                        results.Add(candidateLv);
                }
            }

            results.Sort();
            var deduped = new List<long>(results.Count);
            for (var i = 0; i < results.Count; i++)
                if (i == 0 || results[i] != results[i - 1])
                    deduped.Add(results[i]);
            return deduped;
        }
    }
}
