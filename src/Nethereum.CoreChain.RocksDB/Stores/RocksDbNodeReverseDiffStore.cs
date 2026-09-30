using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;
using RocksDbSharp;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbNodeReverseDiffStore
    {
        private static readonly byte[] ABSENT = new byte[0];
        private static readonly Nethereum.Util.HashProviders.IHashProvider _hashProvider = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();

        private readonly RocksDbManager _manager;
        private readonly bool _buildKeyMajorIndex;

        public RocksDbNodeReverseDiffStore(RocksDbManager manager, bool buildKeyMajorIndex = false)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _buildKeyMajorIndex = buildKeyMajorIndex;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "RocksDbNodeReverseDiffStore.RecordAndCommit - journal the reverse diff and the forward move in one batch")]
        public void RecordAndCommit(ulong block, RocksDbPathTrieNodeStore pathStore, TrieNodeSet set)
        {
            if (pathStore == null) throw new ArgumentNullException(nameof(pathStore));
            if (set == null) return;

            using var batch = _manager.CreateWriteBatch();
            AddRecordToBatch(batch, block, pathStore, set);
            _manager.Write(batch);
        }

        internal void AddRecordToBatch(WriteBatch batch, ulong block, RocksDbPathTrieNodeStore pathStore, TrieNodeSet set, WindowDirtyLayers windowLayers = null)
        {
            AddJournalRecordToBatch(batch, block, pathStore, set, windowLayers);
            AddStateMoveToBatch(batch, pathStore, set);
        }

        internal void AddJournalRecordToBatch(WriteBatch batch, ulong block, RocksDbPathTrieNodeStore pathStore, TrieNodeSet set, WindowDirtyLayers windowLayers = null, HashSet<byte[]> stagedRoots = null, HashSet<byte[]> stagedJournalKeys = null)
        {
            var logCf = _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY);
            var idxCf = _buildKeyMajorIndex ? _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY_INDEX) : null;
            var stagedInThisCall = stagedJournalKeys ?? new HashSet<byte[]>(ByteArrayComparer.Current);

            foreach (var node in set.Nodes)
            {
                var rlp = node.GetEncodedData();
                if (rlp == null || rlp.Length < 32) continue;
                var blockKey = BlockKey(block, node.Owner, node.Path);
                if (_manager.KeyExists(RocksDbManager.CF_NODE_HISTORY, blockKey)) continue;
                if (!stagedInThisCall.Add(blockKey)) continue;
                byte[] prev;
                if (windowLayers != null && windowLayers.TryGetPreImage(IsAccount(node.Owner), node.Owner, node.Path, forBlock: block, out var img, out var absent))
                    prev = absent ? null : img;
                else
                    prev = pathStore.TryGetRaw(node.Owner, node.Path);
                batch.Put(blockKey, prev ?? ABSENT, logCf);
                if (idxCf != null) batch.Put(IndexKey(node.Owner, node.Path, block), ABSENT, idxCf);
            }

            foreach (var d in set.Deletes)
            {
                if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                var blockKey = BlockKey(block, d.Owner, d.Path);
                if (_manager.KeyExists(RocksDbManager.CF_NODE_HISTORY, blockKey)) continue;
                if (!stagedInThisCall.Add(blockKey)) continue;
                batch.Put(blockKey, d.PrevBlob ?? ABSENT, logCf);
                if (idxCf != null) batch.Put(IndexKey(d.Owner, d.Path, block), ABSENT, idxCf);
            }

            if (_buildKeyMajorIndex)
            {
                var rootIdxCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ROOT_INDEX);
                var roots = stagedRoots ?? new HashSet<byte[]>(ByteArrayComparer.Current);
                foreach (var node in set.Nodes)
                {
                    bool isAccountRoot = (node.Owner == null || node.Owner.Length == 0) && (node.Path == null || node.Path.Length == 0);
                    if (!isAccountRoot) continue;
                    var rootRlp = node.GetEncodedData();
                    if (rootRlp != null && rootRlp.Length >= 32)
                    {
                        var rootHash = _hashProvider.ComputeHash(rootRlp);
                        if (!_manager.KeyExists(RocksDbManager.CF_STATE_ROOT_INDEX, rootHash) && roots.Add(rootHash))
                            batch.Put(rootHash, RocksDbManager.Write64BE(block), rootIdxCf);
                    }
                    break;
                }
            }
        }

        internal void AddStateMoveToBatch(WriteBatch batch, RocksDbPathTrieNodeStore pathStore, TrieNodeSet set)
        {
            pathStore.AppendCommit(batch, set);
        }

        private static bool IsAccount(byte[] owner) => owner == null || owner.Length == 0;

        public void RecordAndDeleteRange(ulong block, RocksDbPathTrieNodeStore pathStore, byte[] owner)
        {
            if (pathStore == null) throw new ArgumentNullException(nameof(pathStore));
            if (owner == null || owner.Length == 0) return;

            using var batch = _manager.CreateWriteBatch();
            var logCf = _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY);
            var idxCf = _buildKeyMajorIndex ? _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY_INDEX) : null;
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);

            using var it = _manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            while (it.Valid())
            {
                var key = it.Key();
                if (!ByteUtil.StartsWith(key, owner)) break;
                var blob = it.Value();
                var path = new byte[key.Length - owner.Length];
                Buffer.BlockCopy(key, owner.Length, path, 0, path.Length);
                var blockKey = BlockKey(block, owner, path);
                if (!_manager.KeyExists(RocksDbManager.CF_NODE_HISTORY, blockKey))
                {
                    batch.Put(blockKey, blob ?? ABSENT, logCf);
                    if (idxCf != null) batch.Put(IndexKey(owner, path, block), ABSENT, idxCf);
                }
                batch.Delete(key, storCf);
                it.Next();
            }

            _manager.Write(batch);
        }

        internal void AddRangeWipeToBatch(WriteBatch batch, ulong block, RocksDbPathTrieNodeStore pathStore, byte[] owner, WindowDirtyLayers windowLayers = null, HashSet<byte[]> stagedJournalKeys = null)
        {
            if (pathStore == null) throw new ArgumentNullException(nameof(pathStore));
            if (owner == null || owner.Length == 0) return;

            var logCf = _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY);
            var idxCf = _buildKeyMajorIndex ? _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY_INDEX) : null;
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            var stagedInThisCall = stagedJournalKeys ?? new HashSet<byte[]>(ByteArrayComparer.Current);

            var windowTouches = windowLayers?.EnumerateOwner(owner, block);
            var visitedFromWindow = windowTouches != null && windowTouches.Count > 0
                ? new HashSet<byte[]>(ByteArrayComparer.Current)
                : null;
            var priorWipeInWindow = windowLayers != null && windowLayers.TryGetOwnerWipeBarrier(owner, block, out _);

            using (var it = _manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE))
            {
                it.Seek(owner);
                while (it.Valid())
                {
                    var key = it.Key();
                    if (!ByteUtil.StartsWith(key, owner)) break;
                    var diskBlob = it.Value();
                    var path = new byte[key.Length - owner.Length];
                    Buffer.BlockCopy(key, owner.Length, path, 0, path.Length);

                    var journalValue = diskBlob;
                    var skipJournal = false;
                    if (windowTouches != null && windowTouches.TryGetValue(path, out var touch))
                    {
                        visitedFromWindow.Add(path);
                        if (touch.IsTombstone) skipJournal = true;
                        else journalValue = touch.Value;
                    }
                    else if (priorWipeInWindow)
                    {
                        skipJournal = true;
                    }

                    if (!skipJournal)
                    {
                        var blockKey = BlockKey(block, owner, path);
                        if (!_manager.KeyExists(RocksDbManager.CF_NODE_HISTORY, blockKey) && stagedInThisCall.Add(blockKey))
                        {
                            batch.Put(blockKey, journalValue ?? ABSENT, logCf);
                            if (idxCf != null) batch.Put(IndexKey(owner, path, block), ABSENT, idxCf);
                        }
                    }
                    batch.Delete(key, storCf);
                    it.Next();
                }
            }

            if (windowTouches != null)
            {
                foreach (var kv in windowTouches)
                {
                    var path = kv.Key;
                    if (visitedFromWindow != null && visitedFromWindow.Contains(path)) continue;
                    var touch = kv.Value;
                    if (touch.IsTombstone) continue;

                    var blockKey = BlockKey(block, owner, path);
                    if (!_manager.KeyExists(RocksDbManager.CF_NODE_HISTORY, blockKey) && stagedInThisCall.Add(blockKey))
                    {
                        batch.Put(blockKey, touch.Value ?? ABSENT, logCf);
                        if (idxCf != null) batch.Put(IndexKey(owner, path, block), ABSENT, idxCf);
                    }
                }
            }
        }

        public sealed class NodeRewindStats
        {
            public ulong TargetBlock { get; init; }
            public ulong MaxHistoryBlock { get; set; }
            public ulong MinHistoryBlock { get; set; }
            public long EntriesApplied { get; set; }
            public long Puts { get; set; }
            public long Deletes { get; set; }
        }

        public NodeRewindStats MaterializingRewindTo(ulong targetBlock)
        {
            var stats = new NodeRewindStats { TargetBlock = targetBlock };
            var acctCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);

            using var it = _manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToLast();

            var batch = _manager.CreateWriteBatch();
            int inBatch = 0;
            try
            {
                while (it.Valid())
                {
                    var key = it.Key();
                    var block = RocksDbManager.Read64BE(key);
                    if (block <= targetBlock) break;
                    if (block > stats.MaxHistoryBlock) stats.MaxHistoryBlock = block;
                    stats.MinHistoryBlock = block;

                    var cf = key[8] == (byte)'A' ? acctCf : storCf;
                    var physLen = key.Length - 9;
                    var physKey = new byte[physLen];
                    if (physLen > 0) Buffer.BlockCopy(key, 9, physKey, 0, physLen);

                    var val = it.Value();
                    if (val == null || val.Length == 0) { batch.Delete(physKey, cf); stats.Deletes++; }
                    else { batch.Put(physKey, val, cf); stats.Puts++; }
                    stats.EntriesApplied++;

                    if (++inBatch >= 20000) { _manager.Write(batch); batch.Dispose(); batch = _manager.CreateWriteBatch(); inBatch = 0; }
                    it.Prev();
                }
                if (inBatch > 0) _manager.Write(batch);
            }
            finally { batch.Dispose(); }
            return stats;
        }

        public ulong? FindBlockByStateRoot(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return null;
            var v = _manager.Get(RocksDbManager.CF_STATE_ROOT_INDEX, stateRoot);
            if (v == null || v.Length < 8) return null;
            return RocksDbManager.Read64BE(v);
        }

        public void PruneBelow(ulong floorBlock)
        {
            var floor = RocksDbManager.Write64BE(floorBlock);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY);

            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!BlockBelow(key, floor)) break;
                batch.Delete(key, cf);
                iterator.Next();
            }

            _manager.Write(batch);

            if (_buildKeyMajorIndex) PruneIndexBelow(floorBlock);
            if (_buildKeyMajorIndex) PruneStateRootIndexBelow(floorBlock);
        }

        private void PruneStateRootIndexBelow(ulong floorBlock)
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_ROOT_INDEX);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ROOT_INDEX);
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var v = iterator.Value();
                if (v != null && v.Length >= 8 && RocksDbManager.Read64BE(v) < floorBlock)
                    batch.Delete(iterator.Key(), cf);
                iterator.Next();
            }
            _manager.Write(batch);
        }

        private void PruneIndexBelow(ulong floorBlock)
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY_INDEX);

            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length >= 8 && ReadBlockSuffix(key) < floorBlock)
                    batch.Delete(key, cf);
                iterator.Next();
            }

            _manager.Write(batch);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "RocksDbNodeReverseDiffStore.FindBlobAsOf - a trie node's value as of a past block")]
        public byte[] FindBlobAsOf(byte[] owner, byte[] path, ulong targetN)
        {
            var prefix = IndexPrefix(owner, path);
            var seekKey = new byte[prefix.Length + 8];
            Buffer.BlockCopy(prefix, 0, seekKey, 0, prefix.Length);
            Buffer.BlockCopy(RocksDbManager.Write64BE(targetN + 1), 0, seekKey, prefix.Length, 8);

            using var it = _manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            it.Seek(seekKey);

            if (it.Valid())
            {
                var key = it.Key();
                if (ByteUtil.StartsWith(key, prefix))
                {
                    var b = ReadBlockSuffix(key);
                    return _manager.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(b, owner, path)) ?? ABSENT;
                }
            }
            return null;
        }

        private static byte[] BlockKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            var pathLen = path?.Length ?? 0;
            var ownerLen = acct ? 0 : owner.Length;
            var key = new byte[8 + 1 + ownerLen + pathLen];
            var blockBe = RocksDbManager.Write64BE(block);
            Buffer.BlockCopy(blockBe, 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, key, o, ownerLen); o += ownerLen; }
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, o, pathLen);
            return key;
        }

        private static byte[] IndexKey(byte[] owner, byte[] path, ulong block)
        {
            var prefix = IndexPrefix(owner, path);
            var key = new byte[prefix.Length + 8];
            Buffer.BlockCopy(prefix, 0, key, 0, prefix.Length);
            Buffer.BlockCopy(RocksDbManager.Write64BE(block), 0, key, prefix.Length, 8);
            return key;
        }

        private static byte[] IndexPrefix(byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            var ownerLen = acct ? 0 : owner.Length;
            var pathLen = path?.Length ?? 0;
            if (pathLen > 255) throw new ArgumentException("path length exceeds one byte", nameof(path));
            var prefix = new byte[1 + ownerLen + 1 + pathLen];
            int o = 0;
            prefix[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, prefix, o, ownerLen); o += ownerLen; }
            prefix[o++] = (byte)pathLen;
            if (pathLen > 0) Buffer.BlockCopy(path, 0, prefix, o, pathLen);
            return prefix;
        }

        private static ulong ReadBlockSuffix(byte[] key)
        {
            ulong b = 0;
            for (int i = key.Length - 8; i < key.Length; i++) b = (b << 8) | key[i];
            return b;
        }

        private static bool BlockBelow(byte[] key, byte[] floor8)
        {
            for (int i = 0; i < 8; i++)
            {
                if (key[i] < floor8[i]) return true;
                if (key[i] > floor8[i]) return false;
            }
            return false;
        }
    }
}
