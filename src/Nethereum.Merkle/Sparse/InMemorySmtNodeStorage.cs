using System.Collections.Concurrent;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.Merkle.Sparse
{
    public class InMemorySmtNodeStorage : ISmtNodeStorage
    {
        private readonly ConcurrentDictionary<byte[], byte[]> _store =
            new ConcurrentDictionary<byte[], byte[]>(new ByteArrayComparer());

        public Task<byte[]> GetAsync(byte[] hash)
        {
            _store.TryGetValue(hash, out var data);
            return Task.FromResult(data);
        }

        public Task PutAsync(byte[] hash, byte[] data)
        {
            _store[hash] = data;
            return Task.FromResult(0);
        }

        public Task DeleteAsync(byte[] hash)
        {
            _store.TryRemove(hash, out _);
            return Task.FromResult(0);
        }

        public int Count => _store.Count;
    }
}
