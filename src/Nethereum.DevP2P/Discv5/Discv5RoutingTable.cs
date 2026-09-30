using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    public class Discv5RoutingTable
    {
        public class Entry
        {
            public byte[] NodeId { get; set; }

            public IPEndPoint Address { get; set; }

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
            if (d == 0) return;
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
                    bucket.RemoveAt(0);
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
