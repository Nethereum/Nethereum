using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbTrieNodeStore : ITrieNodeStore
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
    }
}
