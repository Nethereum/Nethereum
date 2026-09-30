using Nethereum.Merkle.Binary.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
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
