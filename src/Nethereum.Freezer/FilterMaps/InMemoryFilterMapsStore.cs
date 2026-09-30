using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public sealed class InMemoryFilterMapsStore : IFilterMapsStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<long, byte[]> _baseRows = new Dictionary<long, byte[]>();
        private readonly Dictionary<long, byte[]> _extRows = new Dictionary<long, byte[]>();
        private readonly Dictionary<long, (long BlockNumber, byte[] BlockId)> _lastBlockOfMap =
            new Dictionary<long, (long BlockNumber, byte[] BlockId)>();
        private readonly Dictionary<long, long> _blockLvPointers = new Dictionary<long, long>();
        private FilterMapsRange _range;

        public FilterMapsRange ReadRange() { lock (_lock) return _range; }

        public void WriteRange(FilterMapsRange range) { lock (_lock) _range = range; }

        public byte[] ReadBaseRowGroup(long mapRowIndex) { lock (_lock) return Read(_baseRows, mapRowIndex); }

        public void WriteBaseRowGroup(long mapRowIndex, byte[] value) { lock (_lock) WriteOrDelete(_baseRows, mapRowIndex, value); }

        public byte[] ReadExtRow(long mapRowIndex) { lock (_lock) return Read(_extRows, mapRowIndex); }

        public void WriteExtRow(long mapRowIndex, byte[] value) { lock (_lock) WriteOrDelete(_extRows, mapRowIndex, value); }

        public (long blockNumber, byte[] blockId)? ReadLastBlockOfMap(long mapIndex)
        {
            lock (_lock) return _lastBlockOfMap.TryGetValue(mapIndex, out var v) ? v : ((long, byte[])?)null;
        }

        public void WriteLastBlockOfMap(long mapIndex, long blockNumber, byte[] blockId)
        {
            lock (_lock) _lastBlockOfMap[mapIndex] = (blockNumber, blockId);
        }

        public long? ReadBlockLvPointer(long blockNumber)
        {
            lock (_lock) return _blockLvPointers.TryGetValue(blockNumber, out var v) ? v : (long?)null;
        }

        public void WriteBlockLvPointer(long blockNumber, long lvPointer) { lock (_lock) _blockLvPointers[blockNumber] = lvPointer; }

        private static byte[] Read(Dictionary<long, byte[]> store, long key)
            => store.TryGetValue(key, out var v) ? v : null;

        private static void WriteOrDelete(Dictionary<long, byte[]> store, long key, byte[] value)
        {
            if (value == null || value.Length == 0) store.Remove(key);
            else store[key] = value;
        }
    }
}
