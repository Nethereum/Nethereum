using System;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbTrieNodeStore : ITrieNodeStore, INodeBlobStore
    {
        private readonly RocksDbManager _manager;

        public RocksDbTrieNodeStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        public void Put(byte[] key, byte[] value)
        {
            if (key == null) return;
            _manager.Put(RocksDbManager.CF_TRIE_NODES, key, value);
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;
            return _manager.Get(RocksDbManager.CF_TRIE_NODES, key);
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;
            _manager.Delete(RocksDbManager.CF_TRIE_NODES, key);
        }

        public bool ContainsKey(byte[] key)
        {
            if (key == null) return false;
            return _manager.KeyExists(RocksDbManager.CF_TRIE_NODES, key);
        }

        public void Flush()
        {
            _manager.Flush();
        }

        public void Clear()
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_TRIE_NODES);
            iterator.SeekToFirst();

            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_TRIE_NODES);

            while (iterator.Valid())
            {
                batch.Delete(iterator.Key(), cf);
                iterator.Next();
            }

            _manager.Write(batch);
        }

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null || nodes.Count == 0) return;
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_TRIE_NODES);
            foreach (var node in nodes.Nodes)
                batch.Put(node.GetHash(), node.GetEncodedData(), cf);
            _manager.Write(batch);
        }

        public byte[] Get(Node reference)
            => reference == null ? null : _manager.Get(RocksDbManager.CF_TRIE_NODES, reference.GetHash());

        public bool Contains(Node reference)
            => reference != null && _manager.KeyExists(RocksDbManager.CF_TRIE_NODES, reference.GetHash());

        public void DeleteRange(byte[] owner)
        {
            if (owner == null || owner.Length == 0) return;

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);

            iterator.Seek(owner);
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!ByteUtil.StartsWith(key, owner)) break;
                batch.Delete(key, cf);
                iterator.Next();
            }

            _manager.Write(batch);
        }

        internal static byte[] StorageKey(byte[] owner, byte[] path)
        {
            var pathLen = path?.Length ?? 0;
            var key = new byte[owner.Length + pathLen];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, owner.Length, pathLen);
            return key;
        }
    }
}
