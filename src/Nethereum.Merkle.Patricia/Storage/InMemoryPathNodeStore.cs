using System;
using System.Collections.Generic;
using Nethereum.Util;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
namespace Nethereum.Merkle.Patricia.Storage
{
    public class InMemoryPathNodeStore : ITrieNodeStore
    {
        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public int Count => _storage.Count;

        private static byte[] Key(Node node) => TrieTracer.Key(node.Owner, node.Path);

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes.Nodes)
            {
                var rlp = node.GetEncodedData();
                if (rlp == null || rlp.Length < 32) continue;
                _storage[Key(node)] = rlp;
            }
            foreach (var d in nodes.Deletes)
            {
                if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                _storage.Remove(TrieTracer.Key(d.Owner, d.Path));
            }
        }

        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            if (!_storage.TryGetValue(Key(reference), out var blob) || blob == null) return null;
            TrieNodeVerification.RequireAuthentic(reference, blob);
            return blob;
        }

        public bool Contains(Node reference)
            => reference != null && _storage.ContainsKey(Key(reference));

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            return _storage.TryGetValue(TrieTracer.Key(null, null), out var blob)
                && TrieNodeVerification.RootBlobHashesTo(blob, stateRoot);
        }

        public void Flush() { }

        public void Clear() => _storage.Clear();
    }
}
