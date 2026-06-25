using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Minimal Kademlia-style routing table keyed by discv5 log-distance from
    /// the local node id (per discv5-theory.md §"Node IDs and Distances").
    /// Per-bucket capacity is bounded so a peer flood can't blow memory.
    /// </summary>
    public class Discv5RoutingTable
    {
        /// <summary>
        /// A peer in the routing table. Treat as immutable once inserted —
        /// <see cref="Upsert"/> keys buckets by <see cref="NodeId"/>, so mutating
        /// the byte array after insertion corrupts bucket integrity. Replace
        /// the entry via <see cref="Upsert"/> instead.
        /// </summary>
        public class Entry
        {
            /// <summary>32-byte discv5 node id (<c>keccak256(pubkey-x ‖ pubkey-y)</c>). Do not mutate after insert.</summary>
            public byte[] NodeId { get; set; }

            /// <summary>Last-seen UDP endpoint for this peer.</summary>
            public IPEndPoint Address { get; set; }

            /// <summary>RLP-encoded ENR record carried in the peer's handshake. Do not mutate after insert.</summary>
            public byte[] EnrEncoded { get; set; }
        }

        private readonly byte[] _localId;
        private readonly Dictionary<uint, List<Entry>> _buckets = new();
        private readonly object _lock = new();
        public int BucketCapacity { get; set; } = 16;

        public Discv5RoutingTable(byte[] localNodeId)
        {
            if (localNodeId == null || localNodeId.Length != 32)
                throw new ArgumentException("local node id must be 32 bytes");
            _localId = localNodeId;
        }

        public void Upsert(Entry entry)
        {
            if (entry?.NodeId == null || entry.NodeId.Length != 32) return;
            var d = LogDistance(_localId, entry.NodeId);
            if (d == 0) return; // never store self
            lock (_lock)
            {
                if (!_buckets.TryGetValue(d, out var bucket))
                {
                    bucket = new List<Entry>();
                    _buckets[d] = bucket;
                }
                bucket.RemoveAll(e => ByteUtil.AreEqual(e.NodeId, entry.NodeId));
                bucket.Add(entry);
                if (bucket.Count > BucketCapacity)
                    bucket.RemoveAt(0); // simple LRU: oldest first
            }
        }

        public List<Entry> AtDistance(uint distance)
        {
            lock (_lock)
            {
                if (!_buckets.TryGetValue(distance, out var b)) return new List<Entry>();
                return new List<Entry>(b);
            }
        }

        /// <summary>
        /// Returns up to <paramref name="k"/> peers ordered by ascending discv5
        /// log-distance from <paramref name="targetNodeId"/>. Standard Kademlia
        /// FIND_NODE primitive — drives the iterative lookup loop.
        /// </summary>
        public List<Entry> Nearest(byte[] targetNodeId, int k)
        {
            if (targetNodeId == null || targetNodeId.Length != 32)
                throw new ArgumentException("target node id must be 32 bytes", nameof(targetNodeId));
            if (k <= 0) return new List<Entry>();
            return Snapshot()
                .OrderBy(e => LogDistance(e.NodeId, targetNodeId))
                .Take(k)
                .ToList();
        }

        public List<Entry> Snapshot()
        {
            lock (_lock)
            {
                return _buckets.Values.SelectMany(b => b).ToList();
            }
        }

        public int Count
        {
            get { lock (_lock) { return _buckets.Values.Sum(b => b.Count); } }
        }

        /// <summary>
        /// discv5 log-distance: <c>256 - leading-zero-bits(a XOR b)</c>.
        /// Two identical ids → distance 0.
        /// </summary>
        public static uint LogDistance(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != 32 || b.Length != 32)
                throw new ArgumentException("ids must be 32 bytes");
            var xor = new byte[32];
            for (int i = 0; i < 32; i++) xor[i] = (byte)(a[i] ^ b[i]);
            return (uint)(256 - ByteUtil.LeadingZeroBits(xor));
        }
    }
}
