using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using RocksDbSharp;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class BufferedTrieStorage : ITrieNodeStore, ITrieWriteBuffer, INodeBlobStore
    {
        private static readonly WriteOptions WalOffWrite = new WriteOptions().DisableWal(1);

        private readonly ITrieNodeStore _inner;
        private readonly ITrieNodeStore _innerNodes;
        private readonly INodeBlobStore _innerBlob;
        private readonly RocksDbManager _manager;
        private readonly bool _walOff;
        private readonly Dictionary<byte[], byte[]> _pending = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
        private readonly Dictionary<NodeIdentity, Node> _pendingNodes = new Dictionary<NodeIdentity, Node>(NodeIdentity.Comparer);

        public BufferedTrieStorage(ITrieNodeStore inner, RocksDbManager manager, bool walOff = false)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _innerNodes = inner as ITrieNodeStore;
            _innerBlob = inner as INodeBlobStore;
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _walOff = walOff;
        }

        public int PendingCount => _pending.Count + _pendingNodes.Count;

        public void Put(byte[] key, byte[] value)
        {
            if (key == null) return;
            _pending[key] = value ?? Array.Empty<byte>();
        }

        public void Delete(byte[] key)
        {
            if (key == null) return;
            _pending[key] = null;
        }

        public byte[] Get(byte[] key)
        {
            if (key == null) return null;
            if (_pending.TryGetValue(key, out var pending))
                return pending;
            return _innerBlob?.Get(key);
        }

        public bool ContainsKey(byte[] key)
        {
            if (key == null) return false;
            if (_pending.TryGetValue(key, out var pending))
                return pending != null;
            return _inner.ContainsKey(key);
        }

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null) return;
            if (_innerNodes == null)
                throw new NotSupportedException(
                    "BufferedTrieStorage.Commit(TrieNodeSet) requires the inner store to implement ITrieNodeStore.");
            foreach (var node in nodes.Nodes)
                _pendingNodes[NodeIdentity.Of(node)] = node;
        }

        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            if (_pendingNodes.TryGetValue(NodeIdentity.Of(reference), out var node))
                return node?.GetEncodedData();
            return _innerNodes != null ? _innerNodes.Get(reference) : _innerBlob?.Get(reference.GetHash());
        }

        public bool Contains(Node reference)
        {
            if (reference == null) return false;
            if (_pendingNodes.TryGetValue(NodeIdentity.Of(reference), out var node))
                return node != null;
            return _innerNodes != null ? _innerNodes.Contains(reference) : _inner.ContainsKey(reference.GetHash());
        }

        public void Flush()
        {
            if (_pending.Count > 0)
            {
                using var batch = _manager.CreateWriteBatch();
                var cf = _manager.GetColumnFamily(RocksDbManager.CF_TRIE_NODES);
                foreach (var kv in _pending)
                {
                    if (kv.Value == null) batch.Delete(kv.Key, cf);
                    else batch.Put(kv.Key, kv.Value, cf);
                }
                _manager.Write(batch, _walOff ? WalOffWrite : null);
                _pending.Clear();
            }

            if (_pendingNodes.Count > 0 && _innerNodes != null)
            {
                var set = new TrieNodeSet();
                foreach (var node in _pendingNodes.Values)
                    if (node != null) set.Add(node);
                _innerNodes.Commit(set);
                _pendingNodes.Clear();
            }
        }

        public void Clear()
        {
            _pending.Clear();
            _pendingNodes.Clear();
            _inner.Clear();
        }

        public void FlushBuffer() => Flush();

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static readonly ByteArrayComparer Instance = new ByteArrayComparer();

            public bool Equals(byte[] a, byte[] b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a == null || b == null || a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }

            public int GetHashCode(byte[] a)
            {
                if (a == null) return 0;
                unchecked
                {
                    int h = (int)2166136261;
                    for (int i = 0; i < a.Length; i++) h = (h ^ a[i]) * 16777619;
                    return h;
                }
            }
        }

        private readonly struct NodeIdentity
        {
            private static readonly byte[] Empty = Array.Empty<byte>();
            public readonly byte[] Owner;
            public readonly byte[] Path;
            public readonly byte[] Hash;

            private NodeIdentity(byte[] owner, byte[] path, byte[] hash)
            {
                Owner = owner;
                Path = path;
                Hash = hash;
            }

            public static NodeIdentity Of(Node node) => new NodeIdentity(node.Owner, node.Path, node.GetHash());

            public static readonly IEqualityComparer<NodeIdentity> Comparer = new Cmp();

            private sealed class Cmp : IEqualityComparer<NodeIdentity>
            {
                private static bool Eq(byte[] a, byte[] b)
                    => ByteArrayComparer.Instance.Equals(a ?? Empty, b ?? Empty);

                public bool Equals(NodeIdentity x, NodeIdentity y)
                    => Eq(x.Hash, y.Hash) && Eq(x.Owner, y.Owner) && Eq(x.Path, y.Path);

                public int GetHashCode(NodeIdentity o)
                    => o.Hash == null ? 0 : ByteArrayComparer.Instance.GetHashCode(o.Hash);
            }
        }
    }
}
