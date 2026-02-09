using Nethereum.Util;
using System.Collections.Generic;



namespace Nethereum.Merkle.Patricia
{
    public class InMemoryTrieStorage : ITrieStorage
    {
        private Dictionary<byte[], byte[]> _storage { get; }

        public Dictionary<byte[], byte[]> Storage { get { return _storage; } }

        public InMemoryTrieStorage()
        {
            _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
        }
        public void Delete(byte[] key)
        {
            _storage.Remove(key);
        }

        public byte[] Get(byte[] key)
        {
            if(_storage.ContainsKey(key)) return _storage[key];
            return null;
        }

        public void Put(byte[] key, byte[] value)
        {
            _storage[key] = value;
        }
    }
}
