using System;
using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public sealed class FilterMapsQueryBackend : IFilterMapsQueryBackend
    {
        private readonly IFilterMapsStore _store;

        public FilterMapsParams Params { get; }

        public FilterMapsQueryBackend(IFilterMapsStore store, FilterMapsParams p)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Params = p;
        }

        public long GetBlockLvPointer(long blockNumber)
        {
            var pointer = _store.ReadBlockLvPointer(blockNumber);
            if (pointer.HasValue)
                return pointer.Value;

            var range = _store.ReadRange();
            if (range != null && blockNumber == range.BlocksAfterLast)
                return range.HeadDelimiter;

            throw new InvalidOperationException(
                $"no block-lv pointer for block {blockNumber}: outside the indexed range");
        }

        public FilterRow GetFilterMapRow(long mapIndex, int rowIndex)
        {
            var range = _store.ReadRange();
            var mapsAfterLast = range?.MapsAfterLast ?? 0;

            var groupSize = Params.BaseRowGroupSize;
            var groupStart = mapIndex - (mapIndex % groupSize);
            var groupLength = (int)Math.Min(groupSize, mapsAfterLast - groupStart);

            var baseRow = FilterRow.Empty;
            if (groupLength > 0)
            {
                var groupMapRowIndex = FilterMapsSchema.MapRowIndex(groupStart, rowIndex, Params);
                var groupBytes = _store.ReadBaseRowGroup(groupMapRowIndex);
                if (groupBytes != null)
                {
                    var rows = FilterMapsRowCodec.DecodeBaseRowGroup(groupBytes, groupLength, Params);
                    baseRow = rows[(int)(mapIndex - groupStart)];
                }
            }

            var extBytes = _store.ReadExtRow(FilterMapsSchema.MapRowIndex(mapIndex, rowIndex, Params));
            var extRow = extBytes == null ? FilterRow.Empty : FilterMapsRowCodec.DecodeExtRow(extBytes, Params);

            if (extRow.Columns.Count == 0)
                return baseRow;

            var combined = new List<uint>(baseRow.Columns.Count + extRow.Columns.Count);
            combined.AddRange(baseRow.Columns);
            combined.AddRange(extRow.Columns);
            return new FilterRow(combined);
        }
    }
}
