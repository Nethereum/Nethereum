using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain
{
    internal sealed class InvalidAncestry
    {
        private const int Capacity = 512;

        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly Queue<string> _order = new Queue<string>();

        private readonly struct Entry
        {
            public Entry(byte[]? latestValidHash, BlockImporterResult? verdict)
            {
                LatestValidHash = latestValidHash;
                Verdict = verdict;
            }

            public byte[]? LatestValidHash { get; }
            public BlockImporterResult? Verdict { get; }
        }

        public void Add(byte[] blockHash, byte[]? latestValidHash, BlockImporterResult? verdict = null)
        {
            var key = blockHash.ToHex();
            lock (_gate)
            {
                if (!_entries.ContainsKey(key))
                {
                    _order.Enqueue(key);
                    if (_order.Count > Capacity) _entries.Remove(_order.Dequeue());
                }
                _entries[key] = new Entry(latestValidHash, verdict);
            }
        }

        public bool TryGet(byte[]? blockHash, out byte[]? latestValidHash)
        {
            latestValidHash = null;
            if (blockHash == null) return false;
            lock (_gate)
            {
                if (!_entries.TryGetValue(blockHash.ToHex(), out var entry)) return false;
                latestValidHash = entry.LatestValidHash;
                return true;
            }
        }

        public bool TryGetVerdict(byte[] blockHash, out BlockImporterResult? verdict)
        {
            verdict = null;
            lock (_gate)
            {
                if (!_entries.TryGetValue(blockHash.ToHex(), out var entry)) return false;
                verdict = entry.Verdict;
                return true;
            }
        }
    }
}
