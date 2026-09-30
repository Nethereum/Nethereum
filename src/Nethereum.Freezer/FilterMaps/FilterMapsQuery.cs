using System;
using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public sealed class FilterMapsQuery
    {
        public IReadOnlyList<IReadOnlyList<byte[]>> Positions { get; }

        public FilterMapsQuery(IReadOnlyList<IReadOnlyList<byte[]>> positions)
        {
            if (positions == null || positions.Count == 0)
                throw new ArgumentException("a filtermaps query needs at least the address position", nameof(positions));
            Positions = positions;
        }
    }
}
