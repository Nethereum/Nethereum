using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatLeafWalk
    {
        private const int LeafWalkBatchSize = 4_096;

        private readonly FlatReconcileEnv _env;
        private readonly CancellationToken _ct;
        private readonly bool _dryRun;
        private readonly long _sampleAccounts;
        private readonly AccountEncoder _decoder = new AccountEncoder();
        private readonly List<PatriciaRangeIterator.RangeEntry> _accountBatch = new();
        private readonly List<PatriciaRangeIterator.RangeEntry> _slotBatch = new();
        private readonly HashSet<byte[]> _codeSeen = new(ByteArrayComparer.Current);
        private readonly HashSet<byte[]> _missingCode = new(ByteArrayComparer.Current);
        private readonly List<(byte[] Owner, byte[] Path)> _missingNodes = new();

        private RocksDbSharp.Iterator _acctIt;
        private RocksDbSharp.Iterator _storIt;
        private byte[] _rangeEnd;
        private byte[] _rangeEnd64;

        private long _accountsScanned, _slotsScanned;
        private long _ghostAccounts, _ghostSlots;
        private long _accountsAdded, _slotsAdded;
        private long _accountsPatched, _slotsPatched;
        private long _rangesWalked;
        private IDictionary<byte[], AccountRoots> _touchedAccounts;

        public List<(byte[] AccountHash, byte[] StorageRoot)> Unresolvable { get; } = new();

        public FlatLeafWalk(FlatReconcileEnv env, CancellationToken ct, bool dryRun, long sampleAccounts)
        {
            _env = env;
            _ct = ct;
            _dryRun = dryRun;
            _sampleAccounts = sampleAccounts;
        }

        public bool MetMissingNodes => _missingNodes.Count > 0 || Unresolvable.Count > 0;

        public ShardResult ToShardResult() => new ShardResult
        {
            Result = new FlatStateReconcileResult(
                _accountsScanned, _slotsScanned,
                _ghostAccounts, _ghostSlots,
                _accountsAdded, _slotsAdded,
                _accountsPatched, _slotsPatched)
            {
                RangesWalked = _rangesWalked,
                LeavesWalked = _accountsScanned + _slotsScanned,
                TrieNodesMissing = _missingNodes.Count,
            },
            Unresolvable = Unresolvable,
            MissingNodes = _missingNodes,
            MissingCode = _missingCode,
        };

        public void ReconcileAccountsOnly(
            byte[] stateRoot, byte[] start, byte[] end, IDictionary<byte[], AccountRoots> touched)
        {
            _touchedAccounts = touched;
            try
            {
                ReconcileAccountRange(stateRoot, start, end);
            }
            finally
            {
                _touchedAccounts = null;
            }
        }

        public void ReconcileStorageSlots(byte[] owner, byte[] storageRoot, byte[] slotStart, byte[] slotEnd)
        {
            _rangesWalked++;
            var bounds = OwnerBounds(owner);
            using var storIt = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE, bounds);
            _storIt = storIt;
            storIt.Seek(FlatStateWalk.OwnerSlotKey(owner, slotStart));
            if (FlatStateWalk.HasStorageRoot(storageRoot))
                ReconcileStorageRange(owner, storageRoot, slotStart, slotEnd);
            else
                DrainGhostStorageOfOwner(owner, slotEnd);
            _storIt = null;
            GC.KeepAlive(bounds);
        }

        private static RocksDbSharp.ReadOptions OwnerBounds(byte[] owner)
        {
            var bounds = new RocksDbSharp.ReadOptions();
            var successor = ByteUtil.PrefixSuccessor(owner);
            return successor == null ? bounds : bounds.SetIterateUpperBound(successor);
        }

        public void ReconcileAccountRange(byte[] stateRoot, byte[] start, byte[] end)
        {
            _rangesWalked++;
            var trieReader = new MissingNodeRecordingStore(_env.TrieNodes);
            var trie = ByteUtil.AreEqual(stateRoot, DefaultValues.EMPTY_TRIE_HASH)
                ? new PatriciaTrie(trieReader)
                : PatriciaTrie.LoadFromStorage(stateRoot, trieReader);
            using var acctIt = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            using var storIt = _touchedAccounts == null ? _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE) : null;
            _acctIt = acctIt;
            _storIt = storIt;
            _rangeEnd = end;
            _rangeEnd64 = end == null ? null : FlatStateWalk.OwnerSlotKey(end, FlatStateWalk.ZeroSlot);
            acctIt.Seek(start);
            storIt?.Seek(FlatStateWalk.OwnerSlotKey(start, FlatStateWalk.ZeroSlot));
            if (!ReconcileTrieAccounts(trie, trieReader, start, end)) return;
            DeleteTailGhostAccounts();
            if (_storIt != null) DrainGhostStorageBelow(_rangeEnd64);
        }

        private bool ReconcileTrieAccounts(PatriciaTrie trie, MissingNodeRecordingStore trieReader, byte[] start, byte[] end)
        {
            using var leaves = PatriciaRangeIterator.EnumerateRange(trie.Root, trieReader, start).GetEnumerator();
            bool more = true;
            while (more)
            {
                more = ReadBatch(leaves, _accountBatch, end);
                if (trieReader.Missing.Count > 0)
                {
                    _missingNodes.AddRange(trieReader.Missing);
                    return false;
                }
                foreach (var leaf in _accountBatch)
                {
                    _ct.ThrowIfCancellationRequested();
                    _accountsScanned++;
                    if (_sampleAccounts > 0 && _accountsScanned > _sampleAccounts) return false;
                    ReconcileAccount(leaf);
                }
            }
            return true;
        }

        private void DeleteTailGhostAccounts()
        {
            byte[] tailKey;
            while ((tailKey = NextPrimaryAccountKey()) != null)
            {
                _ct.ThrowIfCancellationRequested();
                DeleteGhostAccount(tailKey);
            }
        }

        private void DeleteGhostAccount(byte[] flatKey)
        {
            var oldRoot = FlatStateWalk.DecodeFlatAccount(_env.Flat, flatKey, _acctIt.Value())?.StateRoot;
            if (!_dryRun) _env.Flat.DeleteFlatAccountByHash(flatKey);
            _ghostAccounts++;
            Touch(flatKey, oldRoot, DefaultValues.EMPTY_TRIE_HASH);
            _acctIt.Next();
        }

        private void Touch(byte[] accountKey, byte[] oldFlatRoot, byte[] newTrieRoot)
        {
            if (_touchedAccounts != null)
                _touchedAccounts[accountKey] = new AccountRoots(oldFlatRoot, newTrieRoot ?? DefaultValues.EMPTY_TRIE_HASH);
        }

        public void ReconcileOwnerStorage(byte[] owner, byte[] storageRoot)
        {
            _rangesWalked++;
            var bounds = OwnerBounds(owner);
            using var storIt = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE, bounds);
            _storIt = storIt;
            storIt.Seek(owner);
            if (FlatStateWalk.HasStorageRoot(storageRoot))
                ReconcileStorageRange(owner, storageRoot, FlatStateWalk.ZeroSlot, null);
            else
                DrainGhostStorageOfOwner(owner, null);
            _storIt = null;
            GC.KeepAlive(bounds);
        }

        private static bool ReadBatch(
            IEnumerator<PatriciaRangeIterator.RangeEntry> leaves, List<PatriciaRangeIterator.RangeEntry> batch, byte[] end)
        {
            batch.Clear();
            while (batch.Count < LeafWalkBatchSize)
            {
                if (!MoveNextOrStopAtMissingNode(leaves)) return false;
                if (end != null && ByteArrayComparer.Current.Compare(leaves.Current.KeyBytes, end) >= 0) return false;
                batch.Add(leaves.Current);
            }
            return true;
        }

        private static bool MoveNextOrStopAtMissingNode(IEnumerator<PatriciaRangeIterator.RangeEntry> leaves)
        {
            try
            {
                return leaves.MoveNext();
            }
            catch (MissingTrieNodeException)
            {
                return false;
            }
        }

        private void ReconcileAccount(PatriciaRangeIterator.RangeEntry leaf)
        {
            var accountKey = leaf.KeyBytes;

            byte[] flatKey;
            while ((flatKey = NextPrimaryAccountKey()) != null && ByteArrayComparer.Current.Compare(flatKey, accountKey) < 0)
                DeleteGhostAccount(flatKey);

            var trieAccount = _decoder.Decode(leaf.Value);
            _env.CollectMissingCode(trieAccount, _codeSeen, _missingCode);
            ReconcileAccountRow(flatKey, accountKey, leaf.Value, trieAccount);
            if (_storIt == null) return;
            DrainGhostStorageBelow(FlatStateWalk.OwnerSlotKey(accountKey, FlatStateWalk.ZeroSlot));
            if (FlatStateWalk.HasStorage(trieAccount))
                ReconcileStorageRange(accountKey, trieAccount.StateRoot, FlatStateWalk.ZeroSlot, null);
            else
                DrainGhostStorageOfOwner(accountKey, null);
        }

        private void ReconcileStorageRange(byte[] owner, byte[] storageRoot, byte[] slotStart, byte[] slotEnd)
        {
            try
            {
                var trieReader = new MissingNodeRecordingStore(_env.TrieNodes);
                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, trieReader, owner);
                using var slots = PatriciaRangeIterator.EnumerateRange(storageTrie.Root, trieReader, slotStart).GetEnumerator();
                bool more = true;
                while (more)
                {
                    more = ReadBatch(slots, _slotBatch, slotEnd);
                    if (trieReader.Missing.Count > 0)
                    {
                        Unresolvable.Add((owner, storageRoot));
                        _missingNodes.AddRange(trieReader.Missing);
                        SkipStorageOfOwner(owner, slotEnd);
                        return;
                    }
                    foreach (var slotLeaf in _slotBatch)
                        ReconcileSlot(owner, slotLeaf);
                }
                DrainGhostStorageOfOwner(owner, slotEnd);
            }
            catch (InvalidOperationException ex) when (ex is not FlatReconcileUnresolvableNodeException && ex is not ObjectDisposedException)
            {
                Unresolvable.Add((owner, storageRoot));
                SkipStorageOfOwner(owner, slotEnd);
            }
        }

        private void ReconcileSlot(byte[] owner, PatriciaRangeIterator.RangeEntry slotLeaf)
        {
            _slotsScanned++;
            if ((_slotsScanned & 0xFFFF) == 0) _ct.ThrowIfCancellationRequested();

            var slotFlatKey = FlatStateWalk.OwnerSlotKey(owner, slotLeaf.KeyBytes);
            foreach (var ghostKey in OwnerStorageKeysBelow(owner, slotFlatKey))
                DeleteGhostSlot(ghostKey);

            var trieValue = Nethereum.RLP.RLP.Decode(slotLeaf.Value).RLPData;
            if (_storIt.Valid() && ByteArrayComparer.Current.Compare(_storIt.Key(), slotFlatKey) == 0)
            {
                if (!ByteUtil.AreEqual(_storIt.Value(), trieValue))
                {
                    if (!_dryRun) _env.Flat.SaveStorageByHash(owner, slotLeaf.KeyBytes, trieValue);
                    _slotsPatched++;
                }
                _storIt.Next();
            }
            else
            {
                if (!_dryRun) _env.Flat.SaveStorageByHash(owner, slotLeaf.KeyBytes, trieValue);
                _slotsAdded++;
            }
        }

        private void ReconcileAccountRow(byte[] flatKey, byte[] accountKey, byte[] trieLeaf, Account trieAccount)
        {
            if (flatKey != null && ByteArrayComparer.Current.Compare(flatKey, accountKey) == 0)
            {
                var flatAccount = FlatStateWalk.DecodeFlatAccount(_env.Flat, accountKey, _acctIt.Value());
                var flatLeaf = flatAccount == null ? null : AccountEncoder.Current.Encode(flatAccount);
                if (!ByteUtil.AreEqual(flatLeaf, trieLeaf))
                {
                    if (!_dryRun) _env.Flat.SaveAccountByHash(accountKey, trieAccount);
                    _accountsPatched++;
                    Touch(accountKey, flatAccount?.StateRoot, trieAccount?.StateRoot);
                }
                _acctIt.Next();
                return;
            }
            if (!_dryRun) _env.Flat.SaveAccountByHash(accountKey, trieAccount);
            _accountsAdded++;
            Touch(accountKey, null, trieAccount?.StateRoot);
        }

        private byte[] NextPrimaryAccountKey() => FlatStateWalk.NextAccountKey(_acctIt, _rangeEnd);

        private void DrainGhostStorageBelow(byte[] bound64)
        {
            var bound = _rangeEnd64 != null && (bound64 == null || ByteArrayComparer.Current.Compare(_rangeEnd64, bound64) < 0)
                ? _rangeEnd64
                : bound64;
            _ghostSlots += FlatStateWalk.DeleteStorageBelow(_env.Rocks, _storIt, bound, _ct, countOnly: _dryRun);
        }

        private void SkipStorageOfOwner(byte[] owner32, byte[] slotEnd)
        {
            foreach (var _ in OwnerStorageKeysBelow(owner32, slotEnd == null ? null : FlatStateWalk.OwnerSlotKey(owner32, slotEnd)))
            {
            }
        }

        private void DrainGhostStorageOfOwner(byte[] owner32, byte[] slotEnd)
        {
            foreach (var ghostKey in OwnerStorageKeysBelow(owner32, slotEnd == null ? null : FlatStateWalk.OwnerSlotKey(owner32, slotEnd)))
                DeleteGhostSlot(ghostKey);
        }

        private IEnumerable<byte[]> OwnerStorageKeysBelow(byte[] owner32, byte[] bound)
        {
            while (_storIt.Valid())
            {
                var key = _storIt.Key();
                if (!ByteUtil.StartsWith(key, owner32)) yield break;
                if (bound != null && ByteArrayComparer.Current.Compare(key, bound) >= 0) yield break;
                yield return key;
                _storIt.Next();
            }
        }

        private void DeleteGhostSlot(byte[] flatKey)
        {
            if (!_dryRun) _env.DeleteStorageRow(flatKey);
            if ((++_ghostSlots & 0xFFFF) == 0) _ct.ThrowIfCancellationRequested();
        }
    }
}
