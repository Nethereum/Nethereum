using System;
using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public readonly struct FilterRow
    {
        public IReadOnlyList<uint> Columns { get; }

        public FilterRow(IReadOnlyList<uint> columns)
        {
            Columns = columns ?? Array.Empty<uint>();
        }

        public static readonly FilterRow Empty = new FilterRow(Array.Empty<uint>());
    }
}
