using System.Collections.Concurrent;
using Nethereum.Util;

namespace Nethereum.Merkle.Binary.Storage
{
    public class InMemoryBinaryTrieStorage : IBinaryTrieStorage
    {
        private readonly ConcurrentDictionary<byte[], byte[]> _storage =
            new ConcurrentDictionary<byte[], byte[]>(new ByteArrayComparer());

        public void Put(byte[] key, byte[] value)
        {
            _storage[key] = value;
        }

        public byte[] Get(byte[] key)
        {
            _storage.TryGetValue(key, out var value);
            return value;
        }

        public void Delete(byte[] key)
        {
            _storage.TryRemove(key, out _);
        }

        public int Count => _storage.Count;
    }
}
