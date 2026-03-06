using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public class InMemoryTrieNodeStore : ITrieNodeStore
    {
        private readonly ConcurrentDictionary<byte[], byte[]> _storage;

        public InMemoryTrieNodeStore()
        {
            _storage = new ConcurrentDictionary<byte[], byte[]>(new ByteArrayComparer());
        }

        public void Put(byte[] key, byte[] value)
        {
            _storage[key] = value;
        }

        public byte[] Get(byte[] key)
        {
            if (_storage.TryGetValue(key, out var value))
                return value;
            return null;
        }

        public void Delete(byte[] key)
        {
            _storage.TryRemove(key, out _);
        }

        public bool ContainsKey(byte[] key)
        {
            return _storage.ContainsKey(key);
        }

        public void Flush()
        {
        }

        public void Clear()
        {
            _storage.Clear();
        }

        public int Count => _storage.Count;
    }
}
