using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class MissingNodeRecordingStore : ITrieNodeStore
    {
        private readonly ITrieNodeStore _inner;
        private readonly Func<byte[], byte[], (bool Captured, byte[] Blob)> _preImage;
        private readonly List<(byte[] Owner, byte[] Path)> _missing = new();

        public MissingNodeRecordingStore(ITrieNodeStore inner, Func<byte[], byte[], (bool Captured, byte[] Blob)> preImage = null)
        {
            _inner = inner;
            _preImage = preImage;
        }

        public IReadOnlyList<(byte[] Owner, byte[] Path)> Missing => _missing;

        public byte[] Get(Node reference)
        {
            var owner = reference.Owner ?? Array.Empty<byte>();
            var path = reference.Path ?? Array.Empty<byte>();
            var blob = Read(reference, owner, path);
            if (TrieProof.IsAuthentic(blob, (reference as HashNode)?.Hash)) return blob;
            if (_missing.Count < FlatStateWalk.MaxMissingPathsPerStore) _missing.Add((owner, path));
            return null;
        }

        private byte[] Read(Node reference, byte[] owner, byte[] path)
        {
            if (_preImage != null)
            {
                var (captured, preImage) = _preImage(owner, path);
                if (captured) return preImage;
            }
            return TrieProof.TryGet(_inner, reference);
        }

        public bool Contains(Node reference) => _inner.Contains(reference);
        public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        public void Commit(TrieNodeSet nodes) => throw new NotSupportedException("reconcile is read-only over the trie");
        public void Flush() { }
        public void Clear() => throw new NotSupportedException("reconcile is read-only over the trie");
    }
}
