using System.Collections.Generic;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class SstRowBuffer
    {
        private readonly int _threshold;
        private readonly object _lock = new object();
        private List<KeyValuePair<byte[], byte[]>> _rows = new List<KeyValuePair<byte[], byte[]>>();

        public SstRowBuffer(int threshold)
        {
            _threshold = threshold;
        }

        public bool HasRows
        {
            get
            {
                lock (_lock) return _rows.Count > 0;
            }
        }

        public List<KeyValuePair<byte[], byte[]>> Add(
            KeyValuePair<byte[], byte[]> first, KeyValuePair<byte[], byte[]>? second = null)
        {
            lock (_lock)
            {
                _rows.Add(first);
                if (second.HasValue) _rows.Add(second.Value);
                if (_rows.Count < _threshold) return null;
                var full = _rows;
                _rows = new List<KeyValuePair<byte[], byte[]>>(full.Count);
                return full;
            }
        }

        public List<KeyValuePair<byte[], byte[]>> Drain()
        {
            lock (_lock)
            {
                var rows = _rows;
                _rows = new List<KeyValuePair<byte[], byte[]>>();
                return rows;
            }
        }

        public void Clear()
        {
            lock (_lock) { _rows = new List<KeyValuePair<byte[], byte[]>>(); }
        }
    }
}
