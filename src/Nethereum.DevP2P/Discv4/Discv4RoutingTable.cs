using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.DevP2P.Netutil;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv4
{
    public class Discv4RoutingTable
    {
        public const int BucketSize = 16;
        public const int BucketCount = 256;
        public const int MaxReplacements = 10;
        public const int BucketIpLimit = RoutingIpLimits.BucketLimit;
        public const int TableIpLimit = RoutingIpLimits.TableLimit;
        public const int IpLimitSubnetBits = RoutingIpLimits.SubnetBits;

        private readonly byte[] _localIdHash;
        private readonly List<Discv4Node>[] _buckets;
        private readonly List<Discv4Node>[] _replacements;
        private readonly RoutingIpLimits _ipLimits = new RoutingIpLimits();
        private readonly object _lock = new object();

        public Discv4RoutingTable(byte[] localNodeId)
        {
            if (localNodeId == null || localNodeId.Length != 64)
                throw new ArgumentException("Local node ID must be 64 bytes (uncompressed secp256k1 pubkey without 0x04 prefix)");

            _localIdHash = new Sha3Keccack().CalculateHash(localNodeId);
            _buckets = new List<Discv4Node>[BucketCount];
            _replacements = new List<Discv4Node>[BucketCount];
            for (int i = 0; i < BucketCount; i++)
            {
                _buckets[i] = new List<Discv4Node>(BucketSize);
                _replacements[i] = new List<Discv4Node>(MaxReplacements);
            }
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
                    var existing = bucket[existingIndex];
                    _ipLimits.Remove(bucketIndex, existing.IP);
                    if (!_ipLimits.TryAdd(bucketIndex, node.IP))
                    {
                        _ipLimits.TryAdd(bucketIndex, existing.IP);
                        return false;
                    }
                    bucket.RemoveAt(existingIndex);
                    bucket.Insert(0, node);
                    return true;
                }

                if (bucket.Count >= BucketSize)
                {
                    AddReplacement(bucketIndex, node);
                    return false;
                }

                RemoveReplacement(bucketIndex, node.NodeId);
                if (!_ipLimits.TryAdd(bucketIndex, node.IP))
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
                RemoveReplacement(bucketIndex, nodeId);
                var bucket = _buckets[bucketIndex];
                var index = bucket.FindIndex(n => ByteUtil.AreEqual(n.NodeId, nodeId));
                if (index < 0) return false;
                _ipLimits.Remove(bucketIndex, bucket[index].IP);
                bucket.RemoveAt(index);
                PromoteReplacement(bucketIndex);
                return true;
            }
        }

        public Discv4Node NextRevalidationCandidate()
        {
            lock (_lock)
            {
                var occupied = _buckets.Where(b => b.Count > 0).ToList();
                if (occupied.Count == 0) return null;
                var bucket = occupied[System.Security.Cryptography.RandomNumberGenerator.GetInt32(occupied.Count)];
                return bucket[bucket.Count - 1];
            }
        }

        public IList<Discv4Node> ReplacementsFor(byte[] nodeId)
        {
            var bucketIndex = BucketFor(nodeId);
            lock (_lock)
            {
                return _replacements[bucketIndex].ToList();
            }
        }

        private void AddReplacement(int bucketIndex, Discv4Node node)
        {
            var replacements = _replacements[bucketIndex];
            if (replacements.Exists(n => ByteUtil.AreEqual(n.NodeId, node.NodeId))) return;
            if (!_ipLimits.TryAdd(bucketIndex, node.IP)) return;
            replacements.Insert(0, node);
            if (replacements.Count > MaxReplacements)
            {
                _ipLimits.Remove(bucketIndex, replacements[replacements.Count - 1].IP);
                replacements.RemoveAt(replacements.Count - 1);
            }
        }

        private void RemoveReplacement(int bucketIndex, byte[] nodeId)
        {
            var replacements = _replacements[bucketIndex];
            var index = replacements.FindIndex(n => ByteUtil.AreEqual(n.NodeId, nodeId));
            if (index < 0) return;
            _ipLimits.Remove(bucketIndex, replacements[index].IP);
            replacements.RemoveAt(index);
        }

        private void PromoteReplacement(int bucketIndex)
        {
            var replacements = _replacements[bucketIndex];
            var bucket = _buckets[bucketIndex];
            if (replacements.Count == 0 || bucket.Count >= BucketSize) return;
            var index = System.Security.Cryptography.RandomNumberGenerator.GetInt32(replacements.Count);
            bucket.Add(replacements[index]);
            replacements.RemoveAt(index);
        }

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
