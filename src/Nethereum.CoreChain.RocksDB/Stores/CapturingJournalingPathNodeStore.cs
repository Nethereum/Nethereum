using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class CapturingJournalingPathNodeStore :
        ITrieNodeStore, IContractStorageWipeable, IRawNodeReader, ICacheInvalidatable
    {
        private readonly JournalingPathNodeStore _inner;
        private readonly INodeCommitBlockSource _blockSource;
        private static readonly IHashProvider _rootHashProvider = Sha3KeccackHashProvider.Instance;

        private readonly Dictionary<PathKey, (Node Node, ulong Block)> _pendingPuts = new Dictionary<PathKey, (Node, ulong)>(PathKey.Comparer);
        private readonly Dictionary<PathKey, (TrieNodeDelete Delete, ulong Block)> _pendingDeletes = new Dictionary<PathKey, (TrieNodeDelete, ulong)>(PathKey.Comparer);
        private readonly List<(ulong Block, byte[] Owner)> _pendingWipes = new List<(ulong, byte[])>();
        private readonly List<(ulong Block, TrieNodeSet Set)> _perBlockJournal = new List<(ulong, TrieNodeSet)>();
        private ulong? _bufferedBlock;

        private readonly WindowDirtyLayers _inFlightLayers;

        public CapturingJournalingPathNodeStore(
            JournalingPathNodeStore inner, INodeCommitBlockSource blockSource,
            WindowDirtyLayers inFlightLayers = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _blockSource = blockSource ?? throw new ArgumentNullException(nameof(blockSource));
            _inFlightLayers = inFlightLayers;
        }

        public void Commit(TrieNodeSet nodes)
        {
            var block = _blockSource.CurrentBlock;
            if (block.HasValue)
            {
                EnsureArmedBlock(block.Value);
                Coalesce(nodes, block.Value);
                return;
            }

            _inner.Commit(nodes);
        }

        private void EnsureArmedBlock(ulong block)
        {
            if (_bufferedBlock != block)
            {
                if (_bufferedBlock.HasValue)
                    FinalizeBlock(_bufferedBlock.Value);
                _bufferedBlock = block;
            }
        }

        private void FinalizeBlock(ulong endedBlock)
        {
            var set = new TrieNodeSet();
            foreach (var entry in _pendingPuts.Values)
            {
                if (entry.Block == endedBlock)
                    set.Add(entry.Node);
            }
            foreach (var entry in _pendingDeletes.Values)
            {
                if (entry.Block == endedBlock)
                    set.AddDelete(entry.Delete.Owner, entry.Delete.Path, entry.Delete.PrevBlob);
            }
            _perBlockJournal.Add((endedBlock, set));
        }

        private sealed class FrozenNode : Node
        {
            private readonly byte[] _blob;

            public FrozenNode(byte[] owner, byte[] path, byte[] blob) : base(Sha3KeccackHashProvider.Instance)
            {
                Owner = owner;
                Path = path;
                _blob = blob;
            }

            protected override byte[] EncodeCore() => _blob;
        }

        private void Coalesce(TrieNodeSet nodes, ulong block)
        {
            if (nodes == null) return;
            foreach (var node in nodes.Nodes)
            {
                var key = PathKey.Of(node.Owner, node.Path);
                var frozen = new FrozenNode(node.Owner, node.Path, node.GetEncodedData());
                _pendingPuts[key] = (frozen, block);
                _pendingDeletes.Remove(key);
            }
            foreach (var d in nodes.Deletes)
            {
                var key = PathKey.Of(d.Owner, d.Path);
                _pendingDeletes[key] = (d, block);
                _pendingPuts.Remove(key);
            }
        }

        public TrieNodeSet TakeCaptured()
        {
            var set = new TrieNodeSet();
            foreach (var entry in _pendingPuts.Values)
                set.Add(entry.Node);
            foreach (var entry in _pendingDeletes.Values)
                set.AddDelete(entry.Delete.Owner, entry.Delete.Path, entry.Delete.PrevBlob);
            _pendingPuts.Clear();
            _pendingDeletes.Clear();
            _bufferedBlock = null;
            return set;
        }

        public IReadOnlyList<(ulong Block, TrieNodeSet Set)> TakeCapturedJournal()
        {
            if (_bufferedBlock.HasValue)
                FinalizeBlock(_bufferedBlock.Value);
            var result = new List<(ulong, TrieNodeSet)>(_perBlockJournal);
            _perBlockJournal.Clear();
            return result;
        }

        public IReadOnlyList<(ulong Block, byte[] Owner)> TakeCapturedWipes()
        {
            if (_pendingWipes.Count == 0) return Array.Empty<(ulong, byte[])>();
            var wipes = new List<(ulong, byte[])>(_pendingWipes);
            _pendingWipes.Clear();
            return wipes;
        }

        public void DiscardCaptured()
        {
            _pendingPuts.Clear();
            _pendingDeletes.Clear();
            _pendingWipes.Clear();
            _perBlockJournal.Clear();
            _bufferedBlock = null;
        }

        private bool TryGetBuffered(byte[] owner, byte[] path, out byte[] blob, out bool tombstoned)
        {
            if (_bufferedBlock.HasValue)
            {
                var key = PathKey.Of(owner, path);
                if (_pendingDeletes.TryGetValue(key, out _))
                {
                    blob = null;
                    tombstoned = true;
                    return true;
                }
                if (_pendingPuts.TryGetValue(key, out var put))
                {
                    blob = put.Node.GetEncodedData();
                    tombstoned = false;
                    return true;
                }
            }

            if (_inFlightLayers != null &&
                _inFlightLayers.TryGetLatest(owner == null || owner.Length == 0, owner, path, out var latest, out var absent))
            {
                blob = absent ? null : latest;
                tombstoned = absent;
                return true;
            }

            blob = null;
            tombstoned = false;
            return false;
        }

        public byte[] Get(Node reference)
        {
            if (reference != null && TryGetBuffered(reference.Owner, reference.Path, out var blob, out var tombstoned))
            {
                if (tombstoned) return null;
                if (!ProofVerification.Current.TrieNode.Verify(reference.GetHash(), blob, _rootHashProvider))
                    throw new InvalidOperationException(
                        "Path-keyed trie node hash mismatch: stored blob does not match the parent-referenced hash (tampered or corrupt).");
                return blob;
            }
            return _inner.Get(reference);
        }

        public bool Contains(Node reference)
        {
            if (reference != null && TryGetBuffered(reference.Owner, reference.Path, out _, out var tombstoned))
                return !tombstoned;
            return _inner.Contains(reference);
        }

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot != null && stateRoot.Length == 32
                && TryGetBuffered(Array.Empty<byte>(), Array.Empty<byte>(), out var rootBlob, out var tombstoned)
                && !tombstoned && rootBlob != null && rootBlob.Length > 0
                && ByteUtil.AreEqual(_rootHashProvider.ComputeHash(rootBlob), stateRoot))
            {
                return true;
            }
            return _inner.ContainsKey(stateRoot);
        }

        public void Flush() => _inner.Flush();
        public void Clear() => _inner.Clear();

        public void DeleteRange(byte[] owner)
        {
            var block = _blockSource.CurrentBlock;
            if (!block.HasValue)
            {
                _inner.DeleteRange(owner);
                return;
            }

            EnsureArmedBlock(block.Value);
            _pendingWipes.Add((block.Value, owner));
            PurgeOwnerBelow(owner, block.Value);
        }

        private void PurgeOwnerBelow(byte[] owner, ulong wipeBlock)
        {
            if (owner == null || owner.Length == 0) return;

            List<PathKey> stalePuts = null;
            foreach (var kv in _pendingPuts)
            {
                if (kv.Value.Block < wipeBlock && OwnerEquals(kv.Value.Node.Owner, owner))
                    (stalePuts ??= new List<PathKey>()).Add(kv.Key);
            }
            if (stalePuts != null)
                foreach (var key in stalePuts) _pendingPuts.Remove(key);

            List<PathKey> staleDeletes = null;
            foreach (var kv in _pendingDeletes)
            {
                if (kv.Value.Block < wipeBlock && OwnerEquals(kv.Value.Delete.Owner, owner))
                    (staleDeletes ??= new List<PathKey>()).Add(kv.Key);
            }
            if (staleDeletes != null)
                foreach (var key in staleDeletes) _pendingDeletes.Remove(key);
        }

        private static bool OwnerEquals(byte[] a, byte[] b)
        {
            var aLen = a?.Length ?? 0;
            var bLen = b?.Length ?? 0;
            if (aLen != bLen) return false;
            for (int i = 0; i < aLen; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public byte[] TryGetRawNode(byte[] owner, byte[] path)
        {
            if (TryGetBuffered(owner, path, out var blob, out var tombstoned))
                return tombstoned ? null : blob;
            return _inner.TryGetRawNode(owner, path);
        }

        public void ClearCache() => _inner.ClearCache();

        private readonly struct PathKey
        {
            private static readonly byte[] Empty = Array.Empty<byte>();
            private readonly byte[] _owner;
            private readonly byte[] _path;

            private PathKey(byte[] owner, byte[] path)
            {
                _owner = owner ?? Empty;
                _path = path ?? Empty;
            }

            public static PathKey Of(byte[] owner, byte[] path) => new PathKey(owner, path);

            public static readonly IEqualityComparer<PathKey> Comparer = new Cmp();

            private sealed class Cmp : IEqualityComparer<PathKey>
            {
                public bool Equals(PathKey x, PathKey y) => ByteEq(x._owner, y._owner) && ByteEq(x._path, y._path);

                public int GetHashCode(PathKey k)
                {
                    unchecked
                    {
                        int h = (int)2166136261;
                        h = Mix(h, k._owner);
                        h = Mix(h, k._path);
                        return h;
                    }
                }

                private static int Mix(int h, byte[] a)
                {
                    unchecked
                    {
                        for (int i = 0; i < a.Length; i++) h = (h ^ a[i]) * 16777619;
                        return h;
                    }
                }

                private static bool ByteEq(byte[] a, byte[] b)
                {
                    if (ReferenceEquals(a, b)) return true;
                    if (a.Length != b.Length) return false;
                    for (int i = 0; i < a.Length; i++)
                        if (a[i] != b[i]) return false;
                    return true;
                }
            }
        }
    }
}
