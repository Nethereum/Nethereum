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
        {
            var nodes = GetNodesByDepthRange(0, maxDepth);
            var totalSize = 4;
            foreach (var n in nodes)
                totalSize += 4 + n.Hash.Length + 4 + n.Encoded.Length + 4 + 1 + (n.Stem != null ? n.Stem.Length : 0);

            var buffer = new byte[totalSize];
            int offset = 0;

            WriteInt32(buffer, ref offset, nodes.Count);

            foreach (var n in nodes)
            {
                WriteInt32(buffer, ref offset, n.Hash.Length);
                Array.Copy(n.Hash, 0, buffer, offset, n.Hash.Length);
                offset += n.Hash.Length;

                WriteInt32(buffer, ref offset, n.Encoded.Length);
                Array.Copy(n.Encoded, 0, buffer, offset, n.Encoded.Length);
                offset += n.Encoded.Length;

                WriteInt32(buffer, ref offset, n.Depth);
                buffer[offset++] = n.NodeType;

                if (n.Stem != null)
                {
                    Array.Copy(n.Stem, 0, buffer, offset, n.Stem.Length);
                    offset += n.Stem.Length;
                }
            }

            var result = new byte[offset];
            Array.Copy(buffer, result, offset);
            return result;
        }

        public void ImportCheckpoint(byte[] checkpoint)
        {
            if (checkpoint == null || checkpoint.Length < 4) return;

            int offset = 0;
            int count = ReadInt32(checkpoint, ref offset);

            for (int i = 0; i < count; i++)
            {
                int hashLen = ReadInt32(checkpoint, ref offset);
                var hash = new byte[hashLen];
                Array.Copy(checkpoint, offset, hash, 0, hashLen);
                offset += hashLen;

                int encodedLen = ReadInt32(checkpoint, ref offset);
                var encoded = new byte[encodedLen];
                Array.Copy(checkpoint, offset, encoded, 0, encodedLen);
                offset += encodedLen;

                int depth = ReadInt32(checkpoint, ref offset);
                byte nodeType = checkpoint[offset++];

                byte[] stem = null;
                if (nodeType == BinaryTrieConstants.NodeTypeStem)
                {
                    stem = new byte[BinaryTrieConstants.StemSize];
                    Array.Copy(checkpoint, offset, stem, 0, BinaryTrieConstants.StemSize);
                    offset += BinaryTrieConstants.StemSize;
                }

                PutNode(hash, encoded, depth, nodeType, stem);
            }

            ClearDirtyTracking();
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
            offset += 4;
        }

        private static int ReadInt32(byte[] buffer, ref int offset)
        {
            int value = (buffer[offset] << 24) | (buffer[offset + 1] << 16) |
                        (buffer[offset + 2] << 8) | buffer[offset + 3];
            offset += 4;
            return value;
        }
    }
}
