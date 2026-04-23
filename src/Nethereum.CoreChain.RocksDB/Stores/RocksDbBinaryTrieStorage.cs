using Nethereum.Merkle.Binary.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    /// <summary>
    /// Persists EIP-7864 binary-trie nodes in the
    /// <see cref="RocksDbManager.CF_BINARY_TRIE_NODES"/> column family.
    /// Covers the minimal <see cref="IBinaryTrieStorage"/> surface (Put /
    /// Get / Delete). The richer <see cref="IBinaryTrieNodeStore"/> surface
    /// (NodeEntry metadata, depth-range queries, address-stem index, dirty
    /// tracking, checkpointing) requires additional secondary-index keys and
    /// is deferred to a dedicated backlog item — consumers that need the
    /// richer API should use <see cref="InMemoryBinaryTrieNodeStore"/> with
    /// a replay-from-RocksDB warm-up for now.
    /// </summary>
    public class RocksDbBinaryTrieStorage : IBinaryTrieStorage
    {
        private readonly RocksDbManager _manager;

        public RocksDbBinaryTrieStorage(RocksDbManager manager)
        {
            _manager = manager;
        }

        public void Put(byte[] key, byte[] value)
        {
            if (key == null) return;
            _manager.Put(RocksDbManager.CF_BINARY_TRIE_NODES, key, value);
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;
            return _manager.Get(RocksDbManager.CF_BINARY_TRIE_NODES, key);
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;
            _manager.Delete(RocksDbManager.CF_BINARY_TRIE_NODES, key);
        }
    }
}
