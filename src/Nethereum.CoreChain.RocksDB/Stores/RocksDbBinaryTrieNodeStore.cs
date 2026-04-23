using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbBinaryTrieNodeStore : IBinaryTrieNodeStore
    {
        private readonly RocksDbManager _manager;
        private readonly ConcurrentDictionary<byte[], byte> _dirtyHashes =
            new ConcurrentDictionary<byte[], byte>(new ByteArrayComparer());
        private long _currentBlock;

        // Value layout in CF_BINARY_TRIE_NODES:
        //   [depth: 4 BE][nodeType: 1][blockNumber: 8 BE][hasStem: 1][stem: 0|31][encoded: rest]
        private const int HeaderMinSize = 4 + 1 + 8 + 1; // 14 bytes

        public RocksDbBinaryTrieNodeStore(RocksDbManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public int NodeCount
        {
            get
            {
                int count = 0;
                using var it = _manager.CreateIterator(RocksDbManager.CF_BINARY_TRIE_NODES);
                it.SeekToFirst();
                while (it.Valid()) { count++; it.Next(); }
                return count;
            }
        }

        public void Put(byte[] key, byte[] value)
        {
            PutNode(key, value, -1, 0, null);
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;
            var raw = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, key);
            if (raw == null) return null;
            return ExtractEncoded(raw);
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;

            var existing = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, key);
            if (existing != null)
            {
                var entry = ParseValue(key, existing);
                RemoveSecondaryIndexes(entry);
            }

            _manager.Delete(RocksDbManager.CF_BINARY_TRIE_NODES, key);
            _dirtyHashes.TryRemove(key, out _);
        }

        public void PutNode(byte[] hash, byte[] encoded, int depth, byte nodeType, byte[] stem)
        {
            if (hash == null) return;

            using var batch = _manager.CreateWriteBatch();
            var nodesCf = _manager.GetColumnFamily(RocksDbManager.CF_BINARY_TRIE_NODES);
            var depthCf = _manager.GetColumnFamily(RocksDbManager.CF_BINARY_TRIE_DEPTH_IDX);

            var existing = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, hash);
            if (existing != null)
            {
                var old = ParseValue(hash, existing);
                if (old.Depth != depth)
                    batch.Delete(MakeDepthKey(old.Depth, hash), depthCf);
            }

            batch.Put(hash, PackValue(depth, nodeType, _currentBlock, stem, encoded), nodesCf);
            batch.Put(MakeDepthKey(depth, hash), Array.Empty<byte>(), depthCf);
            _manager.Write(batch);

            _dirtyHashes[hash] = 0;
        }

        public void RegisterAddressStem(byte[] address, byte[] stemNodeHash)
        {
            if (address == null || stemNodeHash == null) return;
            var key = MakeAddrStemKey(address, stemNodeHash);
            _manager.Put(RocksDbManager.CF_BINARY_TRIE_ADDR_STEMS, key, Array.Empty<byte>());
        }

        public IReadOnlyList<NodeEntry> GetNodesByDepthRange(int minDepth, int maxDepth)
        {
            var result = new List<NodeEntry>();
            var seekKey = new byte[4];
            seekKey[0] = (byte)(minDepth >> 24);
            seekKey[1] = (byte)(minDepth >> 16);
            seekKey[2] = (byte)(minDepth >> 8);
            seekKey[3] = (byte)minDepth;

            using var it = _manager.CreateIterator(RocksDbManager.CF_BINARY_TRIE_DEPTH_IDX);
            it.Seek(seekKey);

            while (it.Valid())
            {
                var idxKey = it.Key();
                if (idxKey.Length < 4) break;

                int depth = (idxKey[0] << 24) | (idxKey[1] << 16) | (idxKey[2] << 8) | idxKey[3];
                if (depth > maxDepth) break;

                var hash = new byte[idxKey.Length - 4];
                Buffer.BlockCopy(idxKey, 4, hash, 0, hash.Length);

                var raw = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, hash);
                if (raw != null)
                    result.Add(ParseValue(hash, raw));

                it.Next();
            }

            return result;
        }

        public IReadOnlyList<NodeEntry> GetStemNodesByAddress(byte[] address)
        {
            if (address == null || address.Length == 0)
                return Array.Empty<NodeEntry>();

            var result = new List<NodeEntry>();
            var prefix = address;

            using var it = _manager.CreateIterator(RocksDbManager.CF_BINARY_TRIE_ADDR_STEMS);
            it.Seek(prefix);

            while (it.Valid())
            {
                var key = it.Key();
                if (key.Length < prefix.Length || !Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;

                var stemHash = new byte[key.Length - prefix.Length];
                Buffer.BlockCopy(key, prefix.Length, stemHash, 0, stemHash.Length);

                var raw = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, stemHash);
                if (raw != null)
                    result.Add(ParseValue(stemHash, raw));

                it.Next();
            }

            return result;
        }

        public IReadOnlyList<NodeEntry> GetDirtyNodes()
        {
            var result = new List<NodeEntry>();
            foreach (var hash in _dirtyHashes.Keys)
            {
                var raw = _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, hash);
                if (raw != null)
                    result.Add(ParseValue(hash, raw));
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
        }

        public byte[] ExportCheckpoint(int maxDepth)
            => BinaryTrieCheckpointSerializer.Export(GetNodesByDepthRange(0, maxDepth));

        public void ImportCheckpoint(byte[] checkpoint)
        {
            foreach (var entry in BinaryTrieCheckpointSerializer.Import(checkpoint))
                PutNode(entry.Hash, entry.Encoded, entry.Depth, entry.NodeType, entry.Stem);

            ClearDirtyTracking();
        }

        // --- Value packing ---

        private static byte[] PackValue(int depth, byte nodeType, long blockNumber, byte[] stem, byte[] encoded)
        {
            int stemLen = stem?.Length ?? 0;
            int hasStem = stemLen > 0 ? 1 : 0;
            int totalLen = HeaderMinSize + stemLen * hasStem + (encoded?.Length ?? 0);
            var buf = new byte[totalLen];
            int off = 0;

            buf[off++] = (byte)(depth >> 24);
            buf[off++] = (byte)(depth >> 16);
            buf[off++] = (byte)(depth >> 8);
            buf[off++] = (byte)depth;

            buf[off++] = nodeType;

            buf[off++] = (byte)(blockNumber >> 56);
            buf[off++] = (byte)(blockNumber >> 48);
            buf[off++] = (byte)(blockNumber >> 40);
            buf[off++] = (byte)(blockNumber >> 32);
            buf[off++] = (byte)(blockNumber >> 24);
            buf[off++] = (byte)(blockNumber >> 16);
            buf[off++] = (byte)(blockNumber >> 8);
            buf[off++] = (byte)blockNumber;

            buf[off++] = (byte)hasStem;
            if (hasStem == 1 && stem != null)
            {
                Array.Copy(stem, 0, buf, off, stem.Length);
                off += stem.Length;
            }

            if (encoded != null && encoded.Length > 0)
                Array.Copy(encoded, 0, buf, off, encoded.Length);

            return buf;
        }

        private static NodeEntry ParseValue(byte[] hash, byte[] raw)
        {
            if (raw == null || raw.Length < HeaderMinSize)
                return new NodeEntry { Hash = hash, Encoded = raw };

            int off = 0;
            int depth = (raw[off] << 24) | (raw[off + 1] << 16) | (raw[off + 2] << 8) | raw[off + 3];
            off += 4;
            byte nodeType = raw[off++];
            long blockNumber =
                ((long)raw[off] << 56) | ((long)raw[off + 1] << 48) | ((long)raw[off + 2] << 40) |
                ((long)raw[off + 3] << 32) | ((long)raw[off + 4] << 24) | ((long)raw[off + 5] << 16) |
                ((long)raw[off + 6] << 8) | raw[off + 7];
            off += 8;
            byte hasStem = raw[off++];
            byte[] stem = null;
            if (hasStem == 1 && raw.Length >= off + BinaryTrieConstants.StemSize)
            {
                stem = new byte[BinaryTrieConstants.StemSize];
                Array.Copy(raw, off, stem, 0, BinaryTrieConstants.StemSize);
                off += BinaryTrieConstants.StemSize;
            }

            int encodedLen = raw.Length - off;
            byte[] encoded = null;
            if (encodedLen > 0)
            {
                encoded = new byte[encodedLen];
                Array.Copy(raw, off, encoded, 0, encodedLen);
            }

            return new NodeEntry
            {
                Hash = hash,
                Encoded = encoded,
                Depth = depth,
                NodeType = nodeType,
                Stem = stem,
                BlockNumber = blockNumber,
                IsDirty = false
            };
        }

        private static byte[] ExtractEncoded(byte[] raw)
        {
            if (raw == null || raw.Length < HeaderMinSize) return raw;
            int off = HeaderMinSize;
            if (raw[HeaderMinSize - 1] == 1)
                off += BinaryTrieConstants.StemSize;
            int len = raw.Length - off;
            if (len <= 0) return null;
            var result = new byte[len];
            Array.Copy(raw, off, result, 0, len);
            return result;
        }

        // --- Secondary index keys ---

        private static byte[] MakeDepthKey(int depth, byte[] hash)
        {
            var key = new byte[4 + hash.Length];
            key[0] = (byte)(depth >> 24);
            key[1] = (byte)(depth >> 16);
            key[2] = (byte)(depth >> 8);
            key[3] = (byte)depth;
            Buffer.BlockCopy(hash, 0, key, 4, hash.Length);
            return key;
        }

        private static byte[] MakeAddrStemKey(byte[] address, byte[] hash)
        {
            var key = new byte[address.Length + hash.Length];
            Buffer.BlockCopy(address, 0, key, 0, address.Length);
            Buffer.BlockCopy(hash, 0, key, address.Length, hash.Length);
            return key;
        }

        private void RemoveSecondaryIndexes(NodeEntry entry)
        {
            if (entry == null) return;
            var depthKey = MakeDepthKey(entry.Depth, entry.Hash);
            _manager.Delete(RocksDbManager.CF_BINARY_TRIE_DEPTH_IDX, depthKey);
        }

    }
}
