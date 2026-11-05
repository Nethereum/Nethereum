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
            RocksDbPathTrieNodeStore.WipeCf(_manager, RocksDbManager.CF_TRIE_NODES);
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

        [Obsolete("The hash-keyed store has no per-owner storage; use RocksDbPathTrieNodeStore.DeleteRange.")]
        public void DeleteRange(byte[] owner)
        {
            new RocksDbPathTrieNodeStore(_manager).DeleteRange(owner);
        }
    }
}
