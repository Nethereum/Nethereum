using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.DevP2P.Netutil;
using Nethereum.Model.Enr;
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

            public int Failures { get; set; }
        }

        private readonly byte[] _localId;
        private readonly Dictionary<uint, List<Entry>> _buckets = new();
        private readonly Dictionary<uint, List<Entry>> _replacements = new();
        private readonly RoutingIpLimits _ipLimits = new RoutingIpLimits();
        private readonly object _lock = new();
        public const int BucketIpLimit = RoutingIpLimits.BucketLimit;
        public const int TableIpLimit = RoutingIpLimits.TableLimit;
        public const int IpLimitSubnetBits = RoutingIpLimits.SubnetBits;
        public const int MaxReplacements = 10;
        public const int MaxFailures = 3;
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
                    _replacements[d] = new List<Entry>();
                }

                var existingIndex = bucket.FindIndex(e => ByteUtil.AreEqual(e.NodeId, entry.NodeId));
                if (existingIndex >= 0)
                {
                    var existing = bucket[existingIndex];
                    if (SequenceOf(entry) < SequenceOf(existing)) return;
                    _ipLimits.Remove((int)d, existing.Address?.Address);
                    if (!_ipLimits.TryAdd((int)d, entry.Address?.Address))
                    {
                        _ipLimits.TryAdd((int)d, existing.Address?.Address);
                        return;
                    }
                    bucket.RemoveAt(existingIndex);
                    bucket.Add(entry);
                    return;
                }

                if (bucket.Count >= BucketCapacity)
                {
                    AddReplacement(d, entry);
                    return;
                }
                RemoveReplacement(d, entry.NodeId);
                if (!_ipLimits.TryAdd((int)d, entry.Address?.Address)) return;
                bucket.Add(entry);
            }
        }

        public bool Remove(byte[] nodeId)
        {
            if (nodeId == null || nodeId.Length != 32) return false;
            var d = LogDistance(_localId, nodeId);
            lock (_lock)
            {
                return RemoveLocked(d, nodeId);
            }
        }

        public bool RecordFailure(byte[] nodeId)
        {
            if (nodeId == null || nodeId.Length != 32) return false;
            var d = LogDistance(_localId, nodeId);
            lock (_lock)
            {
                if (!_buckets.TryGetValue(d, out var bucket)) return false;
                var entry = bucket.Find(e => ByteUtil.AreEqual(e.NodeId, nodeId));
                if (entry == null) return false;
                entry.Failures++;
                return entry.Failures >= MaxFailures && RemoveLocked(d, nodeId);
            }
        }

        public void RecordSuccess(byte[] nodeId)
        {
            if (nodeId == null || nodeId.Length != 32) return;
            var d = LogDistance(_localId, nodeId);
            lock (_lock)
            {
                if (!_buckets.TryGetValue(d, out var bucket)) return;
                var entry = bucket.Find(e => ByteUtil.AreEqual(e.NodeId, nodeId));
                if (entry != null) entry.Failures = 0;
            }
        }

        public List<Entry> ReplacementsAtDistance(uint distance)
        {
            lock (_lock)
            {
                if (!_replacements.TryGetValue(distance, out var r)) return new List<Entry>();
                return new List<Entry>(r);
            }
        }

        private bool RemoveLocked(uint d, byte[] nodeId)
        {
            if (!_buckets.TryGetValue(d, out var bucket)) return false;
            RemoveReplacement(d, nodeId);
            var index = bucket.FindIndex(e => ByteUtil.AreEqual(e.NodeId, nodeId));
            if (index < 0) return false;
            _ipLimits.Remove((int)d, bucket[index].Address?.Address);
            bucket.RemoveAt(index);
            PromoteReplacement(d);
            return true;
        }

        private void AddReplacement(uint d, Entry entry)
        {
            var replacements = _replacements[d];
            if (replacements.Exists(e => ByteUtil.AreEqual(e.NodeId, entry.NodeId))) return;
            if (!_ipLimits.TryAdd((int)d, entry.Address?.Address)) return;
            replacements.Insert(0, entry);
            if (replacements.Count > MaxReplacements)
            {
                _ipLimits.Remove((int)d, replacements[replacements.Count - 1].Address?.Address);
                replacements.RemoveAt(replacements.Count - 1);
            }
        }

        private void RemoveReplacement(uint d, byte[] nodeId)
        {
            var replacements = _replacements[d];
            var index = replacements.FindIndex(e => ByteUtil.AreEqual(e.NodeId, nodeId));
            if (index < 0) return;
            _ipLimits.Remove((int)d, replacements[index].Address?.Address);
            replacements.RemoveAt(index);
        }

        private void PromoteReplacement(uint d)
        {
            var replacements = _replacements[d];
            var bucket = _buckets[d];
            if (replacements.Count == 0 || bucket.Count >= BucketCapacity) return;
            var index = System.Security.Cryptography.RandomNumberGenerator.GetInt32(replacements.Count);
            bucket.Add(replacements[index]);
            replacements.RemoveAt(index);
        }

        private static ulong SequenceOf(Entry entry)
        {
            if (entry.EnrEncoded == null) return 0;
            try { return EnrRecordEncoder.Decode(entry.EnrEncoded).Sequence; }
            catch (Exception) { return 0; }
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
