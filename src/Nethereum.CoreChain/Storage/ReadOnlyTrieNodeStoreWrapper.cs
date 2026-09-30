using System;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.Storage
{
    public sealed class ReadOnlyTrieNodeStoreWrapper : ITrieNodeStore
    {
        private readonly ITrieNodeStore _inner;

        public ReadOnlyTrieNodeStoreWrapper(ITrieNodeStore inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);

        public void Commit(TrieNodeSet nodes) { }

        public byte[] Get(Node reference) => _inner.Get(reference);

        public bool Contains(Node reference) => _inner.Contains(reference);

        public void Flush() { }
        public void Clear() { }
    }
}
