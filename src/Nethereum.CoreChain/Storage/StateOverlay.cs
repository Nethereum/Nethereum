using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    internal sealed class StateOverlay
    {
        private readonly ConcurrentDictionary<EvmAddress, Account> _accounts = new();
        private readonly ConcurrentDictionary<EvmAddress, byte> _deletedAccounts = new();
        private readonly ConcurrentDictionary<EvmAddress, ConcurrentDictionary<BigInteger, byte[]>> _storage = new();
        private readonly ConcurrentDictionary<EvmAddress, ConcurrentDictionary<BigInteger, byte>> _deletedSlots = new();
        private readonly ConcurrentDictionary<EvmAddress, byte> _clearedStorage = new();
        private readonly ConcurrentDictionary<byte[], byte[]> _code = new(ByteArrayComparer.Current);

        private readonly ConcurrentDictionary<EvmAddress, byte> _dirtyAccounts = new();
        private readonly ConcurrentDictionary<EvmAddress, ConcurrentDictionary<BigInteger, byte>> _dirtyStorageSlots = new();
        private readonly ConcurrentDictionary<EvmAddress, byte> _storageClearedAddresses = new();
        private readonly ConcurrentDictionary<EvmAddress, byte> _absentInBase = new();

        private readonly object _journalGate = new();
        private readonly List<UndoEntry> _journal = new();
        private readonly Stack<OverlaySnapshot> _openSnapshots = new();
        private OverlaySnapshot _committedOutermost;
        private int _openSnapshotCount;
        private int _journaling;
        private int _nextSnapshotId;
        private volatile IDirtyKeyObserver _observer;

        public bool TryAttachObserver(IDirtyKeyObserver observer)
            => Interlocked.CompareExchange(ref _observer, observer, null) == null;

        public void DetachObserver(IDirtyKeyObserver observer)
            => Interlocked.CompareExchange(ref _observer, null, observer);

        private const int KeyBytes = 64;

        public static byte[] CodeKey(byte[] codeHash) => codeHash ?? Array.Empty<byte>();

        public async Task<bool> AccountExistsAsync(IStateStore inner, EvmAddress address)
        {
            var resolved = AccountExists(address);
            if (resolved.HasValue) return resolved.Value;
            var exists = await inner.AccountExistsAsync(address).ConfigureAwait(false);
            if (!exists) NoteAbsentInBase(address);
            return exists;
        }

        public void NoteAbsentInBase(EvmAddress address) => _absentInBase.TryAdd(address, 0);

        public bool IsAbsentInBase(EvmAddress address) => _absentInBase.ContainsKey(address);

        public async Task<Dictionary<string, Account>> MergeAllAccountsAsync(IStateStore inner)
        {
            var merged = await inner.GetAllAccountsAsync().ConfigureAwait(false);
            foreach (var kv in _accounts)
                merged[kv.Key.ToHexLower()] = kv.Value.Clone();
            foreach (var deleted in _deletedAccounts.Keys)
                merged.Remove(deleted.ToHexLower());
            return merged;
        }

        public async IAsyncEnumerable<KeyValuePair<string, Account>> StreamMergedAccountsAsync(IStateStore inner)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _accounts)
            {
                var original = kv.Key.ToHexLower();
                seen.Add(original);
                yield return new KeyValuePair<string, Account>(original, kv.Value.Clone());
            }
            await foreach (var kv in inner.StreamAccountsAsync().ConfigureAwait(false))
            {
                if (seen.Contains(kv.Key)) continue;
                if (IsAccountDeleted(EvmAddress.FromHex(kv.Key))) continue;
                yield return kv;
            }
        }

        public async Task<Dictionary<byte[], byte[]>> MergeAllStorageAsync(IStateStore inner, EvmAddress address)
        {
            Dictionary<byte[], byte[]> result;
            if (IsStorageCleared(address) || IsAbsentInBase(address))
            {
                result = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
            }
            else
            {
                result = await inner.GetAllStorageAsync(address).ConfigureAwait(false);
                foreach (var slot in DeletedStorageSlots(address))
                    result.Remove(StateKeys.StorageSlotKey(slot));
            }
            foreach (var kv in StorageEntries(address))
                result[StateKeys.StorageSlotKey(kv.Key)] = kv.Value;
            return result;
        }


        public bool TryGetAccount(EvmAddress address, out Account account)
        {
            if (_accounts.TryGetValue(address, out var stored)) { account = stored.Clone(); return true; }
            if (_deletedAccounts.ContainsKey(address)) { account = null; return true; }
            account = null;
            return false;
        }

        public void SaveAccount(EvmAddress address, Account account)
        {
            Set(_accounts, address, account.Clone());
            Remove(_deletedAccounts, address);
            MarkAccountDirty(address);
        }

        public bool? AccountExists(EvmAddress address)
        {
            if (_accounts.ContainsKey(address)) return true;
            if (_deletedAccounts.ContainsKey(address)) return false;
            return null;
        }

        public void DeleteAccount(EvmAddress address)
        {
            Remove(_accounts, address);
            Mark(_deletedAccounts, address);
            Remove(_storage, address);
            Remove(_deletedSlots, address);
            Mark(_clearedStorage, address);
            MarkAccountDirty(address);
            Mark(_storageClearedAddresses, address);
        }

        public bool IsAccountDeleted(EvmAddress address) => _deletedAccounts.ContainsKey(address);


        public bool TryGetStorage(EvmAddress address, BigInteger slot, out byte[] value)
        {
            if (_storage.TryGetValue(address, out var slots) && slots.TryGetValue(slot, out var v))
            {
                value = v;
                return true;
            }
            if (_clearedStorage.ContainsKey(address))
            {
                value = null;
                return true;
            }
            if (_deletedSlots.TryGetValue(address, out var deleted) && deleted.ContainsKey(slot))
            {
                value = null;
                return true;
            }
            value = null;
            return false;
        }

        public void SaveStorage(EvmAddress address, BigInteger slot, byte[] value)
        {
            var slots = SlotsOf(_storage, address);
            MarkAccountDirty(address);
            var dirtySlots = SlotsOf(_dirtyStorageSlots, address);
            if (value == null || ByteUtil.IsZero(value))
            {
                Remove(slots, slot);
                Mark(SlotsOf(_deletedSlots, address), slot);
            }
            else
            {
                Set(slots, slot, value.TrimZeroBytes());
                if (_deletedSlots.TryGetValue(address, out var deleted))
                    Remove(deleted, slot);
            }
            if (Mark(dirtySlots, slot)) _observer?.OnStorageSlotDirty(address, slot);
        }

        public bool IsStorageCleared(EvmAddress address) => _clearedStorage.ContainsKey(address);

        public long ApproxBytes()
        {
            long total = 0;

            foreach (var kv in _accounts)
                total += KeyBytes + AccountBytes(kv.Value);
            total += _deletedAccounts.Count * (long)KeyBytes;

            foreach (var kv in _storage)
                foreach (var slot in kv.Value)
                    total += KeyBytes + 32 + (slot.Value?.Length ?? 0);
            foreach (var kv in _deletedSlots)
                total += KeyBytes + kv.Value.Count * 32;
            total += _clearedStorage.Count * (long)KeyBytes;

            foreach (var kv in _code)
                total += KeyBytes + (kv.Value?.Length ?? 0);

            return total;
        }

        private static long AccountBytes(Account account)
        {
            if (account == null) return 0;
            return 32
                + (account.StateRoot?.Length ?? 0)
                + (account.CodeHash?.Length ?? 0);
        }

        public IEnumerable<KeyValuePair<BigInteger, byte[]>> StorageEntries(EvmAddress address)
        {
            if (_storage.TryGetValue(address, out var slots))
                foreach (var kv in slots) yield return kv;
        }

        public IEnumerable<BigInteger> DeletedStorageSlots(EvmAddress address)
        {
            if (_deletedSlots.TryGetValue(address, out var deleted))
                foreach (var slot in deleted.Keys) yield return slot;
        }

        public void ClearStorage(EvmAddress address)
        {
            Remove(_storage, address);
            Remove(_deletedSlots, address);
            Mark(_clearedStorage, address);
            MarkAccountDirty(address);
            Mark(_storageClearedAddresses, address);
        }


        public bool TryGetCode(byte[] codeHash, out byte[] code) => _code.TryGetValue(CodeKey(codeHash), out code);

        public void SaveCode(byte[] codeHash, byte[] code) => Set(_code, CodeKey(codeHash), code);


        public IEnumerable<EvmAddress> DeletedAccountAddresses() => _deletedAccounts.Keys.Where(a => !IsAbsentInBase(a));

        public IEnumerable<EvmAddress> ClearedStorageAddresses() => _clearedStorage.Keys.Where(a => !IsAbsentInBase(a));

        public IEnumerable<(EvmAddress Address, BigInteger Slot, byte[] Value)> BufferedNonZeroStorage()
        {
            foreach (var kv in _storage)
                foreach (var slot in kv.Value)
                    yield return (kv.Key, slot.Key, slot.Value);
        }

        public IEnumerable<(EvmAddress Address, BigInteger Slot)> IndividuallyDeletedSlotsNotCleared()
        {
            foreach (var kv in _deletedSlots)
            {
                if (_clearedStorage.ContainsKey(kv.Key)) continue;
                foreach (var slot in kv.Value.Keys)
                    yield return (kv.Key, slot);
            }
        }

        public IEnumerable<(EvmAddress Address, Account Account)> BufferedAccounts()
        {
            foreach (var kv in _accounts)
                yield return (kv.Key, kv.Value.Clone());
        }

        public IEnumerable<(byte[] CodeHash, byte[] Code)> BufferedCode()
        {
            foreach (var kv in _code)
                yield return (kv.Key, kv.Value);
        }

        public void ClearWrites()
        {
            RetireJournalForClear(nameof(ClearWrites));
            _accounts.Clear();
            _deletedAccounts.Clear();
            _storage.Clear();
            _deletedSlots.Clear();
            _clearedStorage.Clear();
            _code.Clear();
            _absentInBase.Clear();
        }


        public IReadOnlyCollection<EvmAddress> DirtyAccountAddresses() => _dirtyAccounts.Keys.ToList();

        public IReadOnlyCollection<BigInteger> DirtyStorageSlots(EvmAddress address)
            => _dirtyStorageSlots.TryGetValue(address, out var slots)
                ? slots.Keys.ToList()
                : Array.Empty<BigInteger>();

        public IReadOnlyCollection<EvmAddress> StorageClearedAddresses() => _storageClearedAddresses.Keys.ToList();

        public void ClearDirtyTracking()
        {
            RetireJournalForClear(nameof(ClearDirtyTracking));
            _dirtyAccounts.Clear();
            _dirtyStorageSlots.Clear();
            _storageClearedAddresses.Clear();
        }


        public IStateSnapshot CreateSnapshot()
        {
            lock (_journalGate)
            {
                if (_openSnapshots.Count == 0) DiscardCommittedJournal();
                var snapshot = new OverlaySnapshot(Interlocked.Increment(ref _nextSnapshotId), _journal.Count);
                _openSnapshots.Push(snapshot);
                PublishJournalState();
                return snapshot;
            }
        }

        public void CommitSnapshot(IStateSnapshot snapshot)
        {
            lock (_journalGate)
            {
                if (!CloseThrough(snapshot)) return;
                if (_openSnapshots.Count == 0) _committedOutermost = (OverlaySnapshot)snapshot;
                PublishJournalState();
            }
        }

        public void RestoreSnapshot(IStateSnapshot snapshot)
        {
            if (snapshot is not OverlaySnapshot target) return;
            lock (_journalGate)
            {
                if (!target.IsOpen && !ReferenceEquals(target, _committedOutermost))
                    throw new InvalidOperationException("State snapshot " + target.SnapshotId + " is closed and its journal is gone.");
                CloseThrough(target);
                for (var i = _journal.Count - 1; i >= target.JournalPosition; i--)
                    _journal[i].Undo();
                _journal.RemoveRange(target.JournalPosition, _journal.Count - target.JournalPosition);
                if (_openSnapshots.Count == 0) DiscardCommittedJournal();
                PublishJournalState();
            }
        }

        private bool CloseThrough(IStateSnapshot snapshot)
        {
            if (snapshot is not OverlaySnapshot target || !target.IsOpen) return false;
            OverlaySnapshot closed;
            do
            {
                closed = _openSnapshots.Pop();
                closed.IsOpen = false;
            }
            while (!ReferenceEquals(closed, target));
            return true;
        }

        private void DiscardCommittedJournal()
        {
            _journal.Clear();
            _committedOutermost = null;
            PublishJournalState();
        }

        private void EndCommittedJournalOnFirstWrite()
        {
            if (Volatile.Read(ref _journaling) == 0 || Volatile.Read(ref _openSnapshotCount) != 0) return;
            lock (_journalGate)
                if (_openSnapshots.Count == 0 && _committedOutermost != null)
                    DiscardCommittedJournal();
        }

        private void PublishJournalState()
        {
            Volatile.Write(ref _openSnapshotCount, _openSnapshots.Count);
            Volatile.Write(ref _journaling, _openSnapshots.Count > 0 || _committedOutermost != null ? 1 : 0);
        }

        private void RetireJournalForClear(string operation)
        {
            if (Volatile.Read(ref _openSnapshotCount) > 0)
                throw new InvalidOperationException(operation + " is not allowed while a state snapshot is open.");
            lock (_journalGate)
                DiscardCommittedJournal();
        }

        private void Set<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, TKey key, TValue value)
        {
            Journal(map, key);
            map[key] = value;
        }

        private void Remove<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, TKey key)
        {
            Journal(map, key);
            map.TryRemove(key, out _);
        }

        private bool Mark<TKey>(ConcurrentDictionary<TKey, byte> set, TKey key)
        {
            Journal(set, key);
            return set.TryAdd(key, 0);
        }

        private void MarkAccountDirty(EvmAddress address)
        {
            if (Mark(_dirtyAccounts, address)) _observer?.OnAccountDirty(address);
        }

        private ConcurrentDictionary<BigInteger, TValue> SlotsOf<TValue>(
            ConcurrentDictionary<EvmAddress, ConcurrentDictionary<BigInteger, TValue>> slotsByAddress, EvmAddress key)
        {
            Journal(slotsByAddress, key);
            return slotsByAddress.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, TValue>());
        }

        private void Journal<TKey, TValue>(ConcurrentDictionary<TKey, TValue> map, TKey key)
        {
            EndCommittedJournalOnFirstWrite();
            if (Volatile.Read(ref _journaling) == 0) return;
            var existed = map.TryGetValue(key, out var prior);
            lock (_journalGate)
                _journal.Add(new MapEntryRestore<TKey, TValue>(map, key, existed, prior));
        }

        private abstract class UndoEntry
        {
            public abstract void Undo();
        }

        private sealed class MapEntryRestore<TKey, TValue> : UndoEntry
        {
            private readonly ConcurrentDictionary<TKey, TValue> _map;
            private readonly TKey _key;
            private readonly bool _existed;
            private readonly TValue _prior;

            public MapEntryRestore(ConcurrentDictionary<TKey, TValue> map, TKey key, bool existed, TValue prior)
            {
                _map = map;
                _key = key;
                _existed = existed;
                _prior = prior;
            }

            public override void Undo()
            {
                if (_existed) _map[_key] = _prior;
                else _map.TryRemove(_key, out _);
            }
        }

        private sealed class OverlaySnapshot : IStateSnapshot
        {
            public OverlaySnapshot(int id, int journalPosition)
            {
                SnapshotId = id;
                JournalPosition = journalPosition;
                IsOpen = true;
            }

            public int SnapshotId { get; }
            public int JournalPosition { get; }
            public bool IsOpen { get; set; }

            public void SetAccount(string address, Account account) { }
            public void SetStorage(string address, BigInteger slot, byte[] value) { }
            public void SetCode(byte[] codeHash, byte[] code) { }
            public void DeleteAccount(string address) { }
            public void ClearStorage(string address) { }
            public void Dispose() { }
        }
    }
}
