using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Merkle.Binary.Storage
{
    public class InMemoryBinaryTrieNodeStore : IBinaryTrieNodeStore
    {
        private readonly ConcurrentDictionary<byte[], NodeEntry> _nodes =
            new ConcurrentDictionary<byte[], NodeEntry>(new ByteArrayComparer());

        private readonly ConcurrentDictionary<byte[], byte> _dirtyHashes =
            new ConcurrentDictionary<byte[], byte>(new ByteArrayComparer());

        private readonly ConcurrentDictionary<byte[], List<byte[]>> _addressStemIndex =
            new ConcurrentDictionary<byte[], List<byte[]>>(new ByteArrayComparer());

        private long _currentBlock;

        public int NodeCount => _nodes.Count;

        public void Put(byte[] key, byte[] value)
        {
            PutNode(key, value, -1, 0, null);
        }

        public void PutNode(byte[] hash, byte[] encoded, int depth, byte nodeType, byte[] stem)
        {
            var entry = new NodeEntry
            {
                Hash = hash,
                Encoded = encoded,
                Depth = depth,
                NodeType = nodeType,
                Stem = stem,
                BlockNumber = _currentBlock,
                IsDirty = true
            };
            _nodes[hash] = entry;
            _dirtyHashes[hash] = 0;
        }

        public byte[] Get(byte[] key)
        {
            if (_nodes.TryGetValue(key, out var entry))
                return entry.Encoded;
            return null;
        }

        public void Delete(byte[] key)
        {
            _nodes.TryRemove(key, out _);
            _dirtyHashes.TryRemove(key, out _);
        }

        public IReadOnlyList<NodeEntry> GetNodesByDepthRange(int minDepth, int maxDepth)
        {
            var result = new List<NodeEntry>();
            foreach (var kvp in _nodes)
            {
                var entry = kvp.Value;
                if (entry.Depth >= minDepth && entry.Depth <= maxDepth)
                    result.Add(entry);
            }
            return result;
        }

        public void RegisterAddressStem(byte[] address, byte[] stemNodeHash)
        {
            if (address == null || stemNodeHash == null) return;
            var list = _addressStemIndex.GetOrAdd(address, _ => new List<byte[]>());
            lock (list)
            {
                foreach (var existing in list)
                    if (ByteArrayEquals(existing, stemNodeHash)) return;
                list.Add(stemNodeHash);
            }
        }

        public IReadOnlyList<NodeEntry> GetStemNodesByAddress(byte[] address)
        {
            if (address == null || address.Length == 0)
                return new NodeEntry[0];

            if (!_addressStemIndex.TryGetValue(address, out var stemHashes))
                return new NodeEntry[0];

            var result = new List<NodeEntry>();
            lock (stemHashes)
            {
                foreach (var hash in stemHashes)
                {
                    if (_nodes.TryGetValue(hash, out var entry))
                        result.Add(entry);
                }
            }
            return result;
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public IReadOnlyList<NodeEntry> GetDirtyNodes()
        {
            var result = new List<NodeEntry>();
            foreach (var hash in _dirtyHashes.Keys)
            {
                if (_nodes.TryGetValue(hash, out var entry))
                    result.Add(entry);
            }
            return result;
        }

        public void MarkBlockCommitted(long blockNumber)
        {
            _currentBlock = blockNumber;
        }

        public void ClearDirtyTracking()
        {
            _dirtyHashes.Clear();
            foreach (var kvp in _nodes)
                kvp.Value.IsDirty = false;
        }

        public byte[] ExportCheckpoint(int maxDepth)
            => BinaryTrieCheckpointSerializer.Export(GetNodesByDepthRange(0, maxDepth));

        public void ImportCheckpoint(byte[] checkpoint)
        {
            foreach (var entry in BinaryTrieCheckpointSerializer.Import(checkpoint))
                PutNode(entry.Hash, entry.Encoded, entry.Depth, entry.NodeType, entry.Stem);

            ClearDirtyTracking();
        }
    }
}
