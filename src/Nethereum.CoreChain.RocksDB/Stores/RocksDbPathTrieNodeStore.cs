using System;
using System.Linq;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using RocksDbSharp;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.CoreChain.Storage;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB.Stores
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "RocksDbPathTrieNodeStore - the path-keyed (location-addressed) state-trie node store")]
    public class RocksDbPathTrieNodeStore : ITrieNodeStore, IContractStorageWipeable, IRawNodeReader, ICacheInvalidatable
    {
        private readonly RocksDbManager _manager;

        public RocksDbPathTrieNodeStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        public void ClearCache() { }

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null || (nodes.Count == 0 && nodes.Deletes.Count == 0)) return;
            using var batch = _manager.CreateWriteBatch();
            AppendCommit(batch, nodes);
            _manager.Write(batch);
        }

        internal void AppendCommit(WriteBatch batch, TrieNodeSet nodes)
        {
            if (nodes == null) return;
            var acctCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            foreach (var node in nodes.Nodes)
            {
                var rlp = node.GetEncodedData();
                if (rlp == null || rlp.Length < 32) continue;
                if (IsAccount(node.Owner))
                    batch.Put(node.Path ?? Array.Empty<byte>(), rlp, acctCf);
                else
                    batch.Put(TrieTracer.Key(node.Owner, node.Path), rlp, storCf);
            }
            foreach (var d in nodes.Deletes)
            {
                if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                if (IsAccount(d.Owner))
                    batch.Delete(d.Path ?? Array.Empty<byte>(), acctCf);
                else
                    batch.Delete(TrieTracer.Key(d.Owner, d.Path), storCf);
            }
        }

        internal byte[] TryGetRaw(byte[] owner, byte[] path)
        {
            return IsAccount(owner)
                ? _manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path ?? Array.Empty<byte>())
                : _manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, TrieTracer.Key(owner, path));
        }

        public byte[] TryGetRawNode(byte[] owner, byte[] path) => TryGetRaw(owner, path);


        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            byte[] blob = TryGetRaw(reference.Owner, reference.Path);
            TrieNodeVerification.RequireAuthentic(reference, blob);
            return blob;
        }

        public bool Contains(Node reference)
        {
            if (reference == null) return false;
            return IsAccount(reference.Owner)
                ? _manager.KeyExists(RocksDbManager.CF_STATE_TRIE_ACCOUNT, reference.Path ?? Array.Empty<byte>())
                : _manager.KeyExists(RocksDbManager.CF_STATE_TRIE_STORAGE, TrieTracer.Key(reference.Owner, reference.Path));
        }

        public bool ContainsKey(byte[] stateRoot)
            => TrieNodeVerification.RootBlobHashesTo(TryGetRaw(Array.Empty<byte>(), Array.Empty<byte>()), stateRoot);

        public void Flush() => _manager.Flush();

        public void Clear()
        {
            WipeCf(_manager, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            WipeCf(_manager, RocksDbManager.CF_STATE_TRIE_STORAGE);
        }

        public void DeleteRange(byte[] owner)
        {
            if (owner == null || owner.Length == 0) return;

            var end = OwnerRangeEnd(owner);
            var compactAfterWipe = HoldsAtLeast(owner, end, CompactAfterWipeNodes);
            using (var batch = _manager.CreateWriteBatch())
            {
                batch.DeleteRange(owner, (ulong)owner.Length, end, (ulong)end.Length,
                    _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE));
                _manager.Write(batch);
            }
            if (compactAfterWipe) _manager.CompactKeyRange(RocksDbManager.CF_STATE_TRIE_STORAGE, owner, end);
        }

        private const int CompactAfterWipeNodes = 4_096;

        private bool HoldsAtLeast(byte[] owner, byte[] end, int nodes)
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            int seen = 0;
            for (iterator.Seek(owner); iterator.Valid() && seen < nodes; iterator.Next())
            {
                if (iterator.GetKeySpan().SequenceCompareTo(end) >= 0) return false;
                seen++;
            }
            return seen >= nodes;
        }

        internal static byte[] OwnerRangeEnd(byte[] owner)
            => ByteUtil.PrefixSuccessor(owner) ?? ByteUtil.Merge(owner, Enumerable.Repeat((byte)0xFF, 65).ToArray());

        internal static void WipeCf(RocksDbManager manager, string cfName)
        {
            using var iterator = manager.CreateIterator(cfName);
            using var batch = manager.CreateWriteBatch();
            var cf = manager.GetColumnFamily(cfName);
            iterator.SeekToFirst();
            while (iterator.Valid()) { batch.Delete(iterator.Key(), cf); iterator.Next(); }
            manager.Write(batch);
        }

        private static bool IsAccount(byte[] owner) => owner == null || owner.Length == 0;
    }
}
