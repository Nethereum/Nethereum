using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// <see cref="IStateStore"/> facade that absorbs every mutation in memory
    /// while leaving the underlying store untouched. Used by
    /// <see cref="BlockExecutor.ExecuteAsync"/> when
    /// <see cref="BlockExecutionOptions.ReadOnly"/> is set — typically the
    /// on-demand witness-capture path
    /// (<c>ChainNodeBase.CaptureBlockWitnessAsync</c>).
    ///
    /// <para>Reads cascade: in-memory overlay first, then the wrapped store.
    /// Writes (<see cref="SaveAccountAsync"/>, <see cref="SaveStorageAsync"/>,
    /// <see cref="DeleteAccountAsync"/>, <see cref="ClearStorageAsync"/>,
    /// <see cref="SaveCodeAsync"/>) update the overlay only. Dirty-tracking
    /// and snapshot APIs operate against the overlay so
    /// <see cref="TransactionProcessor"/>'s per-tx Create / Revert / Commit
    /// snapshot dance still works inside read-only execution.</para>
    /// </summary>
    public sealed class ReadOnlyStateStoreWrapper : IStateStore
    {
        private readonly IStateStore _inner;

        // Overlay state. Sentinel `_deletedAccounts` lets a delete on an
        // address that lives only in `_inner` mask the inner read even
        // though we never delete anything from `_inner`. `_deletedSlots`
        // plays the same role at slot granularity: an SSTORE-to-zero on a
        // slot that lives only in `_inner` must read back as zero, not as
        // the inner's pre-block value.
        private readonly ConcurrentDictionary<string, Account> _accounts = new();
        private readonly ConcurrentDictionary<string, byte> _deletedAccounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte[]>> _storage = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> _deletedSlots = new();
        private readonly ConcurrentDictionary<string, byte> _clearedStorage = new();
        private readonly ConcurrentDictionary<string, byte[]> _code = new();
        // keccak(addr) hex → original normalized 20-byte address hex.
        // Lets GetAllAccountsAsync / StreamAccountsAsync surface the original
        // address even though primary maps are keyed by keccak.
        private readonly ConcurrentDictionary<string, string> _addressByAccountHash = new();

        // Dirty tracking — tx loop and rewards/withdrawals mutate accounts;
        // the calculator reads dirty sets via the IStateStore contract.
        // Keyed by the original 20-byte address so callers can pass them
        // straight back to GetAccountAsync without a double-keccak hop.
        private readonly ConcurrentDictionary<string, byte> _dirtyAccounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> _dirtyStorageSlots = new();
        private readonly ConcurrentDictionary<string, byte> _storageClearedAddresses = new();

        private int _nextSnapshotId;

        public ReadOnlyStateStoreWrapper(IStateStore inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private static string Normalize(string address)
            => StateKeys.AccountKeyHex(address);

        private static string OriginalAddress(string address)
            => AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();

        public async Task<Account> GetAccountAsync(string address)
        {
            var key = Normalize(address);
            if (_accounts.TryGetValue(key, out var acc)) return acc;
            if (_deletedAccounts.ContainsKey(key)) return null;
            return await _inner.GetAccountAsync(address).ConfigureAwait(false);
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            var key = Normalize(address);
            var original = OriginalAddress(address);
            _accounts[key] = account;
            _addressByAccountHash[key] = original;
            _deletedAccounts.TryRemove(key, out _);
            _dirtyAccounts.TryAdd(original, 0);
            return Task.CompletedTask;
        }

        public async Task<bool> AccountExistsAsync(string address)
        {
            var key = Normalize(address);
            if (_accounts.ContainsKey(key)) return true;
            if (_deletedAccounts.ContainsKey(key)) return false;
            return await _inner.AccountExistsAsync(address).ConfigureAwait(false);
        }

        public Task DeleteAccountAsync(string address)
        {
            var key = Normalize(address);
            var original = OriginalAddress(address);
            _accounts.TryRemove(key, out _);
            _addressByAccountHash.TryRemove(key, out _);
            _deletedAccounts.TryAdd(key, 0);
            _storage.TryRemove(key, out _);
            _deletedSlots.TryRemove(key, out _);
            _clearedStorage.TryAdd(key, 0);
            _dirtyAccounts.TryAdd(original, 0);
            _storageClearedAddresses.TryAdd(original, 0);
            return Task.CompletedTask;
        }

        public async Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            // Inner already returns original-address keys; layer overlay
            // entries by the original address so the result is uniform.
            var merged = await _inner.GetAllAccountsAsync().ConfigureAwait(false);
            foreach (var kv in _accounts)
            {
                var addr = _addressByAccountHash.TryGetValue(kv.Key, out var original) ? original : kv.Key;
                merged[addr] = kv.Value;
            }
            // _deletedAccounts is keccak-hex; map back to original to mask the inner.
            foreach (var deletedKeccak in _deletedAccounts.Keys)
            {
                if (_addressByAccountHash.TryGetValue(deletedKeccak, out var original))
                    merged.Remove(original);
            }
            return merged;
        }

        public async IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _accounts)
            {
                var addr = _addressByAccountHash.TryGetValue(kv.Key, out var original) ? original : kv.Key;
                seen.Add(addr);
                yield return new KeyValuePair<string, Account>(addr, kv.Value);
            }
            await foreach (var kv in _inner.StreamAccountsAsync().ConfigureAwait(false))
            {
                var keccakKey = Normalize(kv.Key);
                if (seen.Contains(kv.Key)) continue;
                if (_deletedAccounts.ContainsKey(keccakKey)) continue;
                yield return kv;
            }
        }

        public async Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var key = Normalize(address);
            if (_storage.TryGetValue(key, out var slots) && slots.TryGetValue(slot, out var v))
            {
                return v;
            }
            if (_clearedStorage.ContainsKey(key))
            {
                // Storage was wiped in the overlay; inner reads must not bleed through.
                if (_storage.TryGetValue(key, out var rewriteSlots) && rewriteSlots.TryGetValue(slot, out var rv))
                    return rv;
                return null;
            }
            if (_deletedSlots.TryGetValue(key, out var deleted) && deleted.ContainsKey(slot))
            {
                // Slot was explicitly zeroed by a prior SSTORE in this block;
                // the inner store still holds the pre-block value but EVM
                // semantics require we read zero.
                return null;
            }
            return await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            var key = Normalize(address);
            var original = OriginalAddress(address);
            var slots = _storage.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, byte[]>());
            if (value == null || IsAllZero(value))
            {
                slots.TryRemove(slot, out _);
                var deleted = _deletedSlots.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, byte>());
                deleted.TryAdd(slot, 0);
            }
            else
            {
                slots[slot] = value;
                if (_deletedSlots.TryGetValue(key, out var deleted))
                    deleted.TryRemove(slot, out _);
            }
            _dirtyAccounts.TryAdd(original, 0);
            var dirty = _dirtyStorageSlots.GetOrAdd(original, _ => new ConcurrentDictionary<BigInteger, byte>());
            dirty.TryAdd(slot, 0);
            return Task.CompletedTask;
        }

        public async Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address)
        {
            var key = Normalize(address);
            Dictionary<BigInteger, byte[]> result;
            if (_clearedStorage.ContainsKey(key))
            {
                result = new Dictionary<BigInteger, byte[]>();
            }
            else
            {
                result = await _inner.GetAllStorageAsync(address).ConfigureAwait(false);
            }
            if (_storage.TryGetValue(key, out var slots))
            {
                foreach (var kv in slots) result[kv.Key] = kv.Value;
            }
            return result;
        }

        public Task ClearStorageAsync(string address)
        {
            var key = Normalize(address);
            var original = OriginalAddress(address);
            _storage.TryRemove(key, out _);
            _deletedSlots.TryRemove(key, out _);
            _clearedStorage.TryAdd(key, 0);
            _dirtyAccounts.TryAdd(original, 0);
            _storageClearedAddresses.TryAdd(original, 0);
            return Task.CompletedTask;
        }

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            if (_code.TryGetValue(hex, out var c)) return c;
            return await _inner.GetCodeAsync(codeHash).ConfigureAwait(false);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            _code[hex] = code;
            return Task.CompletedTask;
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            var id = System.Threading.Interlocked.Increment(ref _nextSnapshotId);
            var snap = new OverlaySnapshot(
                id,
                this,
                new Dictionary<string, Account>(_accounts),
                new HashSet<string>(_deletedAccounts.Keys),
                CloneStorage(_storage),
                CloneDeletedSlots(_deletedSlots),
                new HashSet<string>(_clearedStorage.Keys),
                new Dictionary<string, byte[]>(_code),
                new HashSet<string>(_dirtyAccounts.Keys),
                CloneDirtySlots(_dirtyStorageSlots),
                new HashSet<string>(_storageClearedAddresses.Keys),
                new Dictionary<string, string>(_addressByAccountHash));
            return Task.FromResult<IStateSnapshot>(snap);
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            // Overlay is the source of truth; commit is a no-op (changes already applied).
            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is OverlaySnapshot s) s.Restore();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(_dirtyAccounts.Keys.ToList());

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            var key = OriginalAddress(address);
            if (_dirtyStorageSlots.TryGetValue(key, out var slots))
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(slots.Keys.ToList());
            return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(_storageClearedAddresses.Keys.ToList());

        public Task ClearDirtyTrackingAsync()
        {
            _dirtyAccounts.Clear();
            _dirtyStorageSlots.Clear();
            _storageClearedAddresses.Clear();
            return Task.CompletedTask;
        }

        private static bool IsAllZero(byte[] v)
        {
            for (int i = 0; i < v.Length; i++) if (v[i] != 0) return false;
            return true;
        }

        private static Dictionary<string, Dictionary<BigInteger, byte[]>> CloneStorage(
            ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte[]>> src)
        {
            var dst = new Dictionary<string, Dictionary<BigInteger, byte[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src) dst[kv.Key] = new Dictionary<BigInteger, byte[]>(kv.Value);
            return dst;
        }

        private static Dictionary<string, HashSet<BigInteger>> CloneDirtySlots(
            ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> src)
        {
            var dst = new Dictionary<string, HashSet<BigInteger>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src) dst[kv.Key] = new HashSet<BigInteger>(kv.Value.Keys);
            return dst;
        }

        private static Dictionary<string, HashSet<BigInteger>> CloneDeletedSlots(
            ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> src)
        {
            var dst = new Dictionary<string, HashSet<BigInteger>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src) dst[kv.Key] = new HashSet<BigInteger>(kv.Value.Keys);
            return dst;
        }

        private sealed class OverlaySnapshot : IStateSnapshot
        {
            private readonly ReadOnlyStateStoreWrapper _owner;
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
                ReadOnlyStateStoreWrapper owner,
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
