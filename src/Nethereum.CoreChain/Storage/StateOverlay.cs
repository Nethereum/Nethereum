using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    internal sealed class StateOverlay
    {
        private readonly ConcurrentDictionary<string, Account> _accounts = new();
        private readonly ConcurrentDictionary<string, byte> _deletedAccounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte[]>> _storage = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> _deletedSlots = new();
        private readonly ConcurrentDictionary<string, byte> _clearedStorage = new();
        private readonly ConcurrentDictionary<string, byte[]> _code = new();
        private readonly ConcurrentDictionary<string, string> _addressByAccountHash = new();

        private readonly ConcurrentDictionary<string, byte> _dirtyAccounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> _dirtyStorageSlots = new();
        private readonly ConcurrentDictionary<string, byte> _storageClearedAddresses = new();

        private int _nextSnapshotId;

        public static string Normalize(string address) => StateKeys.AccountKeyHex(address);

        public static string OriginalAddress(string address)
            => AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();


        public bool TryGetAccount(string key, out Account account)
        {
            if (_accounts.TryGetValue(key, out var stored)) { account = stored.Clone(); return true; }
            if (_deletedAccounts.ContainsKey(key)) { account = null; return true; }
            account = null;
            return false;
        }

        public void SaveAccount(string key, string original, Account account)
        {
            _accounts[key] = account;
            _addressByAccountHash[key] = original;
            _deletedAccounts.TryRemove(key, out _);
            _dirtyAccounts.TryAdd(original, 0);
        }

        public bool? AccountExists(string key)
        {
            if (_accounts.ContainsKey(key)) return true;
            if (_deletedAccounts.ContainsKey(key)) return false;
            return null;
        }

        public void DeleteAccount(string key, string original)
        {
            _accounts.TryRemove(key, out _);
            _addressByAccountHash[key] = original;
            _deletedAccounts.TryAdd(key, 0);
            _storage.TryRemove(key, out _);
            _deletedSlots.TryRemove(key, out _);
            _clearedStorage.TryAdd(key, 0);
            _dirtyAccounts.TryAdd(original, 0);
            _storageClearedAddresses.TryAdd(original, 0);
        }

        public IEnumerable<KeyValuePair<string, Account>> LiveAccountsByOriginalAddress()
        {
            foreach (var kv in _accounts)
            {
                var addr = _addressByAccountHash.TryGetValue(kv.Key, out var original) ? original : kv.Key;
                yield return new KeyValuePair<string, Account>(addr, kv.Value.Clone());
            }
        }

        public IEnumerable<string> DeletedOriginalAddresses()
        {
            foreach (var deletedKey in _deletedAccounts.Keys)
                yield return _addressByAccountHash.TryGetValue(deletedKey, out var original) ? original : deletedKey;
        }

        public bool IsAccountDeletedByKey(string key) => _deletedAccounts.ContainsKey(key);


        public bool TryGetStorage(string key, BigInteger slot, out byte[] value)
        {
            if (_storage.TryGetValue(key, out var slots) && slots.TryGetValue(slot, out var v))
            {
                value = v;
                return true;
            }
            if (_clearedStorage.ContainsKey(key))
            {
                value = null;
                return true;
            }
            if (_deletedSlots.TryGetValue(key, out var deleted) && deleted.ContainsKey(slot))
            {
                value = null;
                return true;
            }
            value = null;
            return false;
        }

        public void SaveStorage(string key, string original, BigInteger slot, byte[] value)
        {
            _addressByAccountHash[key] = original;
            var slots = _storage.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, byte[]>());
            if (value == null || IsAllZero(value))
            {
                slots.TryRemove(slot, out _);
                var deleted = _deletedSlots.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, byte>());
                deleted.TryAdd(slot, 0);
            }
            else
            {
                slots[slot] = value.TrimZeroBytes();
                if (_deletedSlots.TryGetValue(key, out var deleted))
                    deleted.TryRemove(slot, out _);
            }
            _dirtyAccounts.TryAdd(original, 0);
            var dirty = _dirtyStorageSlots.GetOrAdd(original, _ => new ConcurrentDictionary<BigInteger, byte>());
            dirty.TryAdd(slot, 0);
        }

        public bool IsStorageCleared(string key) => _clearedStorage.ContainsKey(key);

        public long ApproxBytes()
        {
            long total = 0;

            foreach (var kv in _accounts)
                total += kv.Key.Length + AccountBytes(kv.Value);
            foreach (var key in _deletedAccounts.Keys)
                total += key.Length;

            foreach (var kv in _storage)
                foreach (var slot in kv.Value)
                    total += kv.Key.Length + 32 + (slot.Value?.Length ?? 0);
            foreach (var kv in _deletedSlots)
                total += kv.Key.Length + kv.Value.Count * 32;
            foreach (var key in _clearedStorage.Keys)
                total += key.Length;

            foreach (var kv in _code)
                total += kv.Key.Length + (kv.Value?.Length ?? 0);

            return total;
        }

        private static long AccountBytes(Account account)
        {
            if (account == null) return 0;
            return 32
                + (account.StateRoot?.Length ?? 0)
                + (account.CodeHash?.Length ?? 0);
        }

        public IEnumerable<KeyValuePair<BigInteger, byte[]>> StorageEntries(string key)
        {
            if (_storage.TryGetValue(key, out var slots))
                foreach (var kv in slots) yield return kv;
        }

        public IEnumerable<BigInteger> DeletedStorageSlots(string key)
        {
            if (_deletedSlots.TryGetValue(key, out var deleted))
                foreach (var slot in deleted.Keys) yield return slot;
        }

        public void ClearStorage(string key, string original)
        {
            _addressByAccountHash[key] = original;
            _storage.TryRemove(key, out _);
            _deletedSlots.TryRemove(key, out _);
            _clearedStorage.TryAdd(key, 0);
            _dirtyAccounts.TryAdd(original, 0);
            _storageClearedAddresses.TryAdd(original, 0);
        }


        public bool TryGetCode(string hex, out byte[] code) => _code.TryGetValue(hex, out code);

        public void SaveCode(string hex, byte[] code) => _code[hex] = code;


        public IEnumerable<string> DeletedAccountOriginalAddresses()
        {
            foreach (var key in _deletedAccounts.Keys)
                yield return _addressByAccountHash.TryGetValue(key, out var original) ? original : key;
        }

        public IEnumerable<string> ClearedStorageOriginalAddresses()
        {
            foreach (var key in _clearedStorage.Keys)
                yield return _addressByAccountHash.TryGetValue(key, out var original) ? original : key;
        }

        public IEnumerable<(string OriginalAddress, BigInteger Slot, byte[] Value)> BufferedNonZeroStorage()
        {
            foreach (var kv in _storage)
            {
                var original = _addressByAccountHash.TryGetValue(kv.Key, out var o) ? o : kv.Key;
                foreach (var slot in kv.Value)
                    yield return (original, slot.Key, slot.Value);
            }
        }

        public IEnumerable<(string OriginalAddress, BigInteger Slot)> IndividuallyDeletedSlotsNotCleared()
        {
            foreach (var kv in _deletedSlots)
            {
                if (_clearedStorage.ContainsKey(kv.Key)) continue;
                var original = _addressByAccountHash.TryGetValue(kv.Key, out var o) ? o : kv.Key;
                foreach (var slot in kv.Value.Keys)
                    yield return (original, slot);
            }
        }

        public IEnumerable<(string OriginalAddress, Account Account)> BufferedAccounts()
        {
            foreach (var kv in _accounts)
            {
                var original = _addressByAccountHash.TryGetValue(kv.Key, out var o) ? o : kv.Key;
                yield return (original, kv.Value.Clone());
            }
        }

        public IEnumerable<(byte[] CodeHash, byte[] Code)> BufferedCode()
        {
            foreach (var kv in _code)
                yield return (kv.Key.Length == 0 ? Array.Empty<byte>() : Convert.FromHexString(kv.Key), kv.Value);
        }

        public void ClearWrites()
        {
            _accounts.Clear();
            _deletedAccounts.Clear();
            _storage.Clear();
            _deletedSlots.Clear();
            _clearedStorage.Clear();
            _code.Clear();
            _addressByAccountHash.Clear();
        }


        public IReadOnlyCollection<string> DirtyAccountAddresses() => _dirtyAccounts.Keys.ToList();

        public IReadOnlyCollection<BigInteger> DirtyStorageSlots(string originalAddress)
            => _dirtyStorageSlots.TryGetValue(originalAddress, out var slots)
                ? slots.Keys.ToList()
                : Array.Empty<BigInteger>();

        public IReadOnlyCollection<string> StorageClearedAddresses() => _storageClearedAddresses.Keys.ToList();

        public void ClearDirtyTracking()
        {
            _dirtyAccounts.Clear();
            _dirtyStorageSlots.Clear();
            _storageClearedAddresses.Clear();
        }

        private static bool IsAllZero(byte[] v)
        {
            for (int i = 0; i < v.Length; i++) if (v[i] != 0) return false;
            return true;
        }


        public void RestoreSnapshot(IStateSnapshot snapshot)
        {
            if (snapshot is OverlaySnapshot s) s.Restore();
        }

        public IStateSnapshot CreateSnapshot()
        {
            var id = System.Threading.Interlocked.Increment(ref _nextSnapshotId);
            return new OverlaySnapshot(
                id,
                this,
                new Dictionary<string, Account>(_accounts),
                new HashSet<string>(_deletedAccounts.Keys),
                CloneStorage(_storage),
                CloneSlotSet(_deletedSlots),
                new HashSet<string>(_clearedStorage.Keys),
                new Dictionary<string, byte[]>(_code),
                new HashSet<string>(_dirtyAccounts.Keys),
                CloneSlotSet(_dirtyStorageSlots),
                new HashSet<string>(_storageClearedAddresses.Keys),
                new Dictionary<string, string>(_addressByAccountHash));
        }

        private static Dictionary<string, Dictionary<BigInteger, byte[]>> CloneStorage(
            ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte[]>> src)
        {
            var dst = new Dictionary<string, Dictionary<BigInteger, byte[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src) dst[kv.Key] = new Dictionary<BigInteger, byte[]>(kv.Value);
            return dst;
        }

        private static Dictionary<string, HashSet<BigInteger>> CloneSlotSet(
            ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> src)
        {
            var dst = new Dictionary<string, HashSet<BigInteger>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src) dst[kv.Key] = new HashSet<BigInteger>(kv.Value.Keys);
            return dst;
        }

        private sealed class OverlaySnapshot : IStateSnapshot
        {
            private readonly StateOverlay _owner;
            private readonly Dictionary<string, Account> _accounts;
            private readonly HashSet<string> _deletedAccounts;
            private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _storage;
            private readonly Dictionary<string, HashSet<BigInteger>> _deletedSlots;
            private readonly HashSet<string> _clearedStorage;
            private readonly Dictionary<string, byte[]> _code;
            private readonly HashSet<string> _dirtyAccounts;
            private readonly Dictionary<string, HashSet<BigInteger>> _dirtyStorageSlots;
            private readonly HashSet<string> _storageClearedAddresses;
            private readonly Dictionary<string, string> _addressByAccountHash;

            public int SnapshotId { get; }

            public OverlaySnapshot(
                int id,
                StateOverlay owner,
                Dictionary<string, Account> accounts,
                HashSet<string> deletedAccounts,
                Dictionary<string, Dictionary<BigInteger, byte[]>> storage,
                Dictionary<string, HashSet<BigInteger>> deletedSlots,
                HashSet<string> clearedStorage,
                Dictionary<string, byte[]> code,
                HashSet<string> dirtyAccounts,
                Dictionary<string, HashSet<BigInteger>> dirtyStorageSlots,
                HashSet<string> storageClearedAddresses,
                Dictionary<string, string> addressByAccountHash)
            {
                SnapshotId = id;
                _owner = owner;
                _accounts = accounts;
                _deletedAccounts = deletedAccounts;
                _storage = storage;
                _deletedSlots = deletedSlots;
                _clearedStorage = clearedStorage;
                _code = code;
                _dirtyAccounts = dirtyAccounts;
                _dirtyStorageSlots = dirtyStorageSlots;
                _storageClearedAddresses = storageClearedAddresses;
                _addressByAccountHash = addressByAccountHash;
            }

            public void Restore()
            {
                _owner._accounts.Clear();
                foreach (var kv in _accounts) _owner._accounts[kv.Key] = kv.Value;
                _owner._deletedAccounts.Clear();
                foreach (var k in _deletedAccounts) _owner._deletedAccounts.TryAdd(k, 0);
                _owner._storage.Clear();
                foreach (var kv in _storage)
                {
                    var slots = new ConcurrentDictionary<BigInteger, byte[]>(kv.Value);
                    _owner._storage[kv.Key] = slots;
                }
                _owner._deletedSlots.Clear();
                foreach (var kv in _deletedSlots)
                {
                    var deleted = new ConcurrentDictionary<BigInteger, byte>();
                    foreach (var s in kv.Value) deleted.TryAdd(s, 0);
                    _owner._deletedSlots[kv.Key] = deleted;
                }
                _owner._clearedStorage.Clear();
                foreach (var k in _clearedStorage) _owner._clearedStorage.TryAdd(k, 0);
                _owner._code.Clear();
                foreach (var kv in _code) _owner._code[kv.Key] = kv.Value;
                _owner._dirtyAccounts.Clear();
                foreach (var k in _dirtyAccounts) _owner._dirtyAccounts.TryAdd(k, 0);
                _owner._dirtyStorageSlots.Clear();
                foreach (var kv in _dirtyStorageSlots)
                {
                    var dirty = new ConcurrentDictionary<BigInteger, byte>();
                    foreach (var s in kv.Value) dirty.TryAdd(s, 0);
                    _owner._dirtyStorageSlots[kv.Key] = dirty;
                }
                _owner._storageClearedAddresses.Clear();
                foreach (var k in _storageClearedAddresses) _owner._storageClearedAddresses.TryAdd(k, 0);
                _owner._addressByAccountHash.Clear();
                foreach (var kv in _addressByAccountHash) _owner._addressByAccountHash[kv.Key] = kv.Value;
            }

            public void SetAccount(string address, Account account) { }
            public void SetStorage(string address, BigInteger slot, byte[] value) { }
            public void SetCode(byte[] codeHash, byte[] code) { }
            public void DeleteAccount(string address) { }
            public void ClearStorage(string address) { }
            public void Dispose() { }
        }
    }
}
