using System;
using System.Collections.Generic;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.Storage
{
    using ProofVerification = Nethereum.Merkle.Patricia.ProofVerification.ProofVerification;
    public class InMemoryPathNodeStore : ITrieNodeStore
    {
        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
        private readonly IHashProvider _hashProvider = Sha3KeccackHashProvider.Instance;

        public int Count => _storage.Count;

        private static byte[] Key(Node node) => Key(node.Owner, node.Path);

        private static byte[] Key(byte[] owner, byte[] path)
        {
            path = path ?? new byte[0];
            if (owner == null || owner.Length == 0) return path;
            var key = new byte[owner.Length + path.Length];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            if (path.Length > 0) Buffer.BlockCopy(path, 0, key, owner.Length, path.Length);
            return key;
        }

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
                _storage.Remove(Key(d.Owner, d.Path));
            }
        }

        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            if (!_storage.TryGetValue(Key(reference), out var blob) || blob == null) return null;
            if (!ProofVerification.Current.TrieNode.Verify(reference.GetHash(), blob, _hashProvider))
                throw new InvalidOperationException(
                    "Path-keyed trie node hash mismatch: stored blob does not match the parent-referenced hash (tampered or corrupt).");
            return blob;
        }

        public bool Contains(Node reference)
            => reference != null && _storage.ContainsKey(Key(reference));

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            if (!_storage.TryGetValue(Key(null, new byte[0]), out var blob) || blob == null || blob.Length == 0)
                return false;
            return ByteUtil.AreEqual(_hashProvider.ComputeHash(blob), stateRoot);
        }

        public void Flush() { }

        public void Clear() => _storage.Clear();
    }
}
