using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.Util;

using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.Storage
{
    public class InMemoryContentNodeStore : INodeBlobStore, ITrieNodeStore
    {
        private readonly ConcurrentDictionary<byte[], byte[]> _storage =
            new ConcurrentDictionary<byte[], byte[]>(new ByteArrayComparer());

        public IDictionary<byte[], byte[]> Storage => _storage;

        public int Count => _storage.Count;

        public void Put(byte[] key, byte[] value)
        {
            _storage[key] = value;
        }

        public byte[] Get(byte[] key)
        {
            if (_storage.TryGetValue(key, out var value)) return value;
            return null;
        }

        public void Delete(byte[] key)
        {
            _storage.TryRemove(key, out _);
        }

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes.Nodes)
                _storage[node.GetHash()] = node.GetEncodedData();
        }

        public byte[] Get(Node reference)
            => reference != null && _storage.TryGetValue(reference.GetHash(), out var v) ? v : null;

        public bool Contains(Node reference)
            => reference != null && _storage.ContainsKey(reference.GetHash());

        public bool ContainsKey(byte[] key)
            => key != null && _storage.ContainsKey(key);

        public void Flush() { }

        public void Clear() => _storage.Clear();
    }
}
