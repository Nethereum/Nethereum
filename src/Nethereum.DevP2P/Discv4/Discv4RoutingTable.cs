using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv4
{
    /// <summary>
    /// Kademlia routing table for discv4. Nodes are organized into 256 k-buckets
    /// indexed by log2-distance from our local node ID. Bucket size k = 16 per
    /// spec. Distance is keccak256(id_a) XOR keccak256(id_b) treated as a
    /// 256-bit big-endian integer.
    /// </summary>
    public class Discv4RoutingTable
    {
        public const int BucketSize = 16;
        public const int BucketCount = 256;

        private readonly byte[] _localIdHash;
        private readonly List<Discv4Node>[] _buckets;
        private readonly object _lock = new object();

        public Discv4RoutingTable(byte[] localNodeId)
        {
            if (localNodeId == null || localNodeId.Length != 64)
                throw new ArgumentException("Local node ID must be 64 bytes (uncompressed secp256k1 pubkey without 0x04 prefix)");

            _localIdHash = new Sha3Keccack().CalculateHash(localNodeId);
            _buckets = new List<Discv4Node>[BucketCount];
            for (int i = 0; i < BucketCount; i++)
                _buckets[i] = new List<Discv4Node>(BucketSize);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _buckets.Sum(b => b.Count);
                }
            }
        }

        public bool Add(Discv4Node node)
        {
            if (node == null || node.NodeId == null || node.NodeId.Length != 64)
                return false;

            var bucketIndex = BucketFor(node.NodeId);
            lock (_lock)
            {
                var bucket = _buckets[bucketIndex];

                var existingIndex = bucket.FindIndex(n => ByteUtil.AreEqual(n.NodeId, node.NodeId));
                if (existingIndex >= 0)
                {
                    bucket.RemoveAt(existingIndex);
                    bucket.Insert(0, node);
                    return true;
                }

                if (bucket.Count >= BucketSize)
                    return false;

                bucket.Insert(0, node);
                return true;
            }
        }

        public bool Remove(byte[] nodeId)
        {
            if (nodeId == null) return false;
            var bucketIndex = BucketFor(nodeId);
            lock (_lock)
            {
                var bucket = _buckets[bucketIndex];
                var index = bucket.FindIndex(n => ByteUtil.AreEqual(n.NodeId, nodeId));
                if (index < 0) return false;
                bucket.RemoveAt(index);
                return true;
            }
        }

        /// <summary>
        /// Returns up to BucketSize closest nodes to the given target by XOR distance.
        /// </summary>
        public IList<Discv4Node> FindClosest(byte[] target, int count = BucketSize)
        {
            var targetHash = new Sha3Keccack().CalculateHash(target);
            List<Discv4Node> all;
            lock (_lock)
            {
                all = _buckets.SelectMany(b => b).ToList();
            }
            return all
                .OrderBy(n => Distance(new Sha3Keccack().CalculateHash(n.NodeId), targetHash), ByteArrayComparer.BigEndianUnsigned)
                .Take(count)
                .ToList();
        }

        public IList<Discv4Node> AllNodes()
        {
            lock (_lock)
            {
                return _buckets.SelectMany(b => b).ToList();
            }
        }

        private int BucketFor(byte[] nodeId)
        {
            var hashedId = new Sha3Keccack().CalculateHash(nodeId);
            var distance = Distance(_localIdHash, hashedId);
            var leadingZeros = ByteUtil.LeadingZeroBits(distance);
            var idx = BucketCount - 1 - leadingZeros;
            if (idx < 0) idx = 0;
            if (idx >= BucketCount) idx = BucketCount - 1;
            return idx;
        }

        private static byte[] Distance(byte[] a, byte[] b)
        {
            var result = new byte[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = (byte)(a[i] ^ b[i]);
            return result;
        }

        private class ByteArrayComparer : IComparer<byte[]>
        {
            public static readonly ByteArrayComparer BigEndianUnsigned = new ByteArrayComparer();
            public int Compare(byte[] x, byte[] y)
            {
                for (int i = 0; i < x.Length; i++)
                {
                    var cmp = x[i].CompareTo(y[i]);
                    if (cmp != 0) return cmp;
                }
                return 0;
            }
        }
    }

    public class Discv4Node
    {
        public byte[] NodeId { get; set; } = new byte[64];
        public IPAddress IP { get; set; } = IPAddress.Loopback;
        public ushort UdpPort { get; set; }
        public ushort TcpPort { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
