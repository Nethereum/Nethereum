using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CheckpointReplay
{
    /// <summary>
    /// Writable in-memory overlay over the read-only checkpoint path-keyed trie node store. Reads resolve
    /// overlay-first then fall through to the checkpoint (verify-on-read: keccak(blob)==reference hash).
    /// Writes (Commit) accumulate in memory so subsequent cold reads within the replay run see this run's
    /// committed nodes, exactly mirroring the follower's latest-only path store — WITHOUT touching the
    /// checkpoint. Behaviour matches <c>RocksDbPathTrieNodeStore</c>.
    /// </summary>
    public sealed class OverlayNodeStore : ITrieNodeStore, IContractStorageWipeable
    {
        private readonly CheckpointReader _base;
        private static readonly IHashProvider _hash = new Sha3KeccackHashProvider();

        // key = owner(hex) || "/" || path(hex) ; value = blob (present) ; membership in _deleted = tombstone
        private readonly Dictionary<string, byte[]> _nodes = new();
        private readonly HashSet<string> _deleted = new();
        private readonly HashSet<string> _wipedOwners = new(); // owner-hex prefixes wiped this run

        public OverlayNodeStore(CheckpointReader baseReader)
        {
            _base = baseReader ?? throw new ArgumentNullException(nameof(baseReader));
        }

        // Cold-read instrumentation: when Recording is on, every node resolution is logged with its source
        // ('B' = served from the checkpoint base [a genuine cold read of the checkpoint's node CFs],
        //  'b' = base miss/null, 'O' = overlay [this run's committed node], 'D' = tombstoned, 'W' = wiped).
        public bool Recording;
        public readonly List<(char Src, string Owner, string Path)> Reads = new();

        private static bool IsAccount(byte[] owner) => owner == null || owner.Length == 0;
        private static string Hex(byte[] b) => b == null ? "" : b.ToHex();
        private static string Key(byte[] owner, byte[] path) => Hex(owner) + "/" + Hex(path);

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes.Nodes)
            {
                var rlp = node.GetEncodedData();
                if (rlp == null || rlp.Length < 32) continue; // embedded, never keyed
                var k = Key(node.Owner, node.Path);
                _nodes[k] = rlp;
                _deleted.Remove(k);
            }
            foreach (var d in nodes.Deletes)
            {
                if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                var k = Key(d.Owner, d.Path);
                _nodes.Remove(k);
                _deleted.Add(k);
            }
        }

        private byte[] GetRaw(byte[] owner, byte[] path)
        {
            var k = Key(owner, path);
            if (_nodes.TryGetValue(k, out var blob)) { if (Recording) Reads.Add(('O', Hex(owner), Hex(path))); return blob; }
            if (_deleted.Contains(k)) { if (Recording) Reads.Add(('D', Hex(owner), Hex(path))); return null; }
            if (owner != null && owner.Length > 0 && _wipedOwners.Contains(Hex(owner)))
            { if (Recording) Reads.Add(('W', Hex(owner), Hex(path))); return null; } // contract-scoped wipe with no re-commit
            var b = _base.GetTrieNode(owner, path);
            if (Recording) Reads.Add((b != null ? 'B' : 'b', Hex(owner), Hex(path)));
            return b;
        }

        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            var blob = GetRaw(reference.Owner, reference.Path);
            if (blob == null) return null;
            var expected = reference.GetHash();
            var actual = _hash.ComputeHash(blob);
            if (!ByteUtil.AreEqual(expected, actual))
                throw new InvalidOperationException(
                    "CORRUPT-NODE Path-keyed trie node hash mismatch (verify-on-read): stored blob does not match parent-referenced hash. "
                    + $"owner={Hex(reference.Owner)} path={Hex(reference.Path)} "
                    + $"expected(parentRef)={Hex(expected)} actual(keccakLiveBlob)={Hex(actual)} "
                    + $"blobLen={blob.Length} blob={Hex(blob)}");
            return blob;
        }

        public bool Contains(Node reference)
        {
            if (reference == null) return false;
            return GetRaw(reference.Owner, reference.Path) != null;
        }

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            var blob = GetRaw(Array.Empty<byte>(), Array.Empty<byte>());
            if (blob == null || blob.Length == 0) return false;
            return ByteUtil.AreEqual(_hash.ComputeHash(blob), stateRoot);
        }

        public void DeleteRange(byte[] owner)
        {
            if (owner == null || owner.Length == 0) return;
            var prefix = Hex(owner);
            _wipedOwners.Add(prefix);
            var toRemove = new List<string>();
            foreach (var k in _nodes.Keys)
                if (KeyOwnerMatches(k, prefix)) toRemove.Add(k);
            foreach (var k in toRemove) _nodes.Remove(k);
        }

        private static bool KeyOwnerMatches(string key, string ownerHex)
        {
            var slash = key.IndexOf('/');
            return slash >= 0 && key.Substring(0, slash) == ownerHex;
        }

        public void Flush() { /* in-memory overlay, nothing to flush */ }
        public void Clear() { _nodes.Clear(); _deleted.Clear(); _wipedOwners.Clear(); }
    }
}
