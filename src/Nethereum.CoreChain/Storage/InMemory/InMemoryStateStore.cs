using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryStateStore : IStateStore, ISnapFlatStateWriter, IRawStorageEnumerator
    {
        private readonly object _snapshotLock = new object();
        private readonly ConcurrentDictionary<string, Account> _accounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte[]>> _storage = new();
        private readonly ConcurrentDictionary<string, byte[]> _code = new();
        private readonly ConcurrentDictionary<string, byte> _dirtyAccounts = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> _dirtyStorageSlots = new();
        private readonly ConcurrentDictionary<string, byte> _storageClearedAddresses = new();
        private readonly ConcurrentDictionary<string, string> _addressByAccountHash = new();
        private int _nextSnapshotId = 0;
        private volatile CowStateSnapshot _activeSnapshot;

        private static string NormalizeAddress(string address)
        {
            return StateKeys.AccountKeyHex(address);
        }

        private static string NormalizeAddress(EvmAddress address)
        {
            return StateKeys.AccountKeyHex(address);
        }

        private static string OriginalAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        private static string OriginalAddress(EvmAddress address)
        {
            return address.ToHexLower();
        }

        public Task<Account> GetAccountAsync(string address) => GetAccountByNormalizedAsync(NormalizeAddress(address));

        public Task<Account> GetAccountAsync(EvmAddress address) => GetAccountByNormalizedAsync(NormalizeAddress(address));

        private Task<Account> GetAccountByNormalizedAsync(string normalizedAddress)
        {
            _accounts.TryGetValue(normalizedAddress, out var account);
            return Task.FromResult(account);
        }

        public Task SaveAccountAsync(string address, Account account)
            => SaveAccountByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), account);

        public Task SaveAccountAsync(EvmAddress address, Account account)
            => SaveAccountByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), account);

        private Task SaveAccountByNormalizedAsync(string normalizedAddress, string originalAddress, Account account)
        {
            var snapshot = _activeSnapshot;
            if (snapshot != null)
            {
                _accounts.TryGetValue(normalizedAddress, out var existing);
                snapshot.SaveAccountUndoIfNeeded(normalizedAddress, existing.Clone());
            }
            _accounts[normalizedAddress] = account;
            _addressByAccountHash[normalizedAddress] = originalAddress;
            _dirtyAccounts.TryAdd(originalAddress, 0);
            return Task.CompletedTask;
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            var normalizedAddress = NormalizeAddress(address);
            return Task.FromResult(_accounts.ContainsKey(normalizedAddress));
        }

        public Task<bool> AccountExistsAsync(EvmAddress address)
        {
            var normalizedAddress = NormalizeAddress(address);
            return Task.FromResult(_accounts.ContainsKey(normalizedAddress));
        }

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
        {
            _accounts[accountHash.ToHex()] = account;
            return Task.CompletedTask;
        }

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
        {
            _accounts.TryGetValue(accountHash.ToHex(), out var account);
            return Task.FromResult(account);
        }

        public Task DeleteAccountByHashAsync(byte[] accountHash)
        {
            _accounts.TryRemove(accountHash.ToHex(), out _);
            return Task.CompletedTask;
        }

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            => throw new NotSupportedException(
                "InMemoryStateStore stores storage by BigInteger slot; snap-flat storage requires a keccak-keyed backend (RocksDB). A cleared slot cannot be honoured here, and silently dropping it would leave a stale row.");

        public Task DeleteAccountAsync(string address)
            => DeleteAccountByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address));

        public Task DeleteAccountAsync(EvmAddress address)
            => DeleteAccountByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address));

        private Task DeleteAccountByNormalizedAsync(string normalizedAddress, string originalAddress)
        {
            var snapshot = _activeSnapshot;
            if (snapshot != null)
            {
                _accounts.TryGetValue(normalizedAddress, out var existing);
                snapshot.SaveAccountUndoIfNeeded(normalizedAddress, existing.Clone());
                SaveStorageClearUndo(snapshot, normalizedAddress);
            }
            _accounts.TryRemove(normalizedAddress, out _);
            _storage.TryRemove(normalizedAddress, out _);
            _addressByAccountHash.TryRemove(normalizedAddress, out _);
            _dirtyAccounts.TryAdd(originalAddress, 0);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var result = new Dictionary<string, Account>();
            foreach (var kv in _accounts)
            {
                var addr = _addressByAccountHash.TryGetValue(kv.Key, out var original) ? original : kv.Key;
                result[addr] = kv.Value;
            }
            return Task.FromResult(result);
        }

#pragma warning disable CS1998
        public async System.Collections.Generic.IAsyncEnumerable<System.Collections.Generic.KeyValuePair<string, Account>> StreamAccountsAsync()
#pragma warning restore CS1998
        {
            foreach (var kv in _accounts)
            {
                var addr = _addressByAccountHash.TryGetValue(kv.Key, out var original) ? original : kv.Key;
                yield return new System.Collections.Generic.KeyValuePair<string, Account>(addr, kv.Value);
            }
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => GetStorageByNormalizedAsync(NormalizeAddress(address), slot);

        public Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot) => GetStorageByNormalizedAsync(NormalizeAddress(address), slot);

        private Task<byte[]> GetStorageByNormalizedAsync(string normalizedAddress, BigInteger slot)
        {
            if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                return Task.FromResult<byte[]>(null);

            accountStorage.TryGetValue(slot, out var value);
            return Task.FromResult(value);
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => SaveStorageByKeccakNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), slotKeccak, value);

        public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value)
            => SaveStorageByKeccakNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), slotKeccak, value);

        private Task SaveStorageByKeccakNormalizedAsync(string normalizedAddress, string originalAddress, byte[] slotKeccak, byte[] value)
        {
            var slot = ResolveSlotFromKeccak(normalizedAddress, slotKeccak);
            if (slot == null) return Task.CompletedTask;

            return SaveStorageByNormalizedAsync(normalizedAddress, originalAddress, slot.Value, value);
        }

        private BigInteger? ResolveSlotFromKeccak(string normalizedAddress, byte[] slotKeccak)
        {
            if (slotKeccak == null) return null;
            if (!_storage.TryGetValue(normalizedAddress, out var accountStorage)) return null;

            foreach (var slot in accountStorage.Keys)
                if (ByteArrayComparer.Current.Equals(StateKeys.StorageSlotKey(slot), slotKeccak))
                    return slot;

            return null;
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => SaveStorageByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), slot, value);

        public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value)
            => SaveStorageByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address), slot, value);

        private Task SaveStorageByNormalizedAsync(string normalizedAddress, string originalAddress, BigInteger slot, byte[] value)
        {
            var snapshot = _activeSnapshot;
            if (snapshot != null)
            {
                byte[] originalValue = null;
                if (_storage.TryGetValue(normalizedAddress, out var existing))
                    existing.TryGetValue(slot, out originalValue);
                snapshot.SaveStorageUndoIfNeeded(normalizedAddress, slot, originalValue?.ToArray());
            }

            var accountStorage = _storage.GetOrAdd(normalizedAddress, _ => new ConcurrentDictionary<BigInteger, byte[]>());

            if (value == null || IsAllZero(value))
                accountStorage.TryRemove(slot, out _);
            else
                accountStorage[slot] = value.TrimZeroBytes();

            _dirtyAccounts.TryAdd(originalAddress, 0);

            var dirtySlots = _dirtyStorageSlots.GetOrAdd(originalAddress, _ => new ConcurrentDictionary<BigInteger, byte>());
            dirtySlots.TryAdd(slot, 0);

            return Task.CompletedTask;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => GetAllStorageByNormalizedAsync(NormalizeAddress(address));

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address) => GetAllStorageByNormalizedAsync(NormalizeAddress(address));

        private Task<Dictionary<byte[], byte[]>> GetAllStorageByNormalizedAsync(string normalizedAddress)
        {
            var result = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
            if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                return Task.FromResult(result);

            foreach (var kv in accountStorage)
                result[StateKeys.StorageSlotKey(kv.Key)] = kv.Value;
            return Task.FromResult(result);
        }

#pragma warning disable CS1998
        public async System.Collections.Generic.IAsyncEnumerable<KeyValuePair<BigInteger, byte[]>>
            StreamRawStorageAsync(string address)
#pragma warning restore CS1998
        {
            var normalizedAddress = NormalizeAddress(address);
            if (!_storage.TryGetValue(normalizedAddress, out var accountStorage))
                yield break;

            foreach (var kv in accountStorage)
                yield return kv;
        }

        public Task ClearStorageAsync(string address)
            => ClearStorageByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address));

        public Task ClearStorageAsync(EvmAddress address)
            => ClearStorageByNormalizedAsync(NormalizeAddress(address), OriginalAddress(address));

        private Task ClearStorageByNormalizedAsync(string normalizedAddress, string originalAddress)
        {
            var snapshot = _activeSnapshot;
            if (snapshot != null)
            {
                SaveStorageClearUndo(snapshot, normalizedAddress);
            }
            _storage.TryRemove(normalizedAddress, out _);
            _dirtyAccounts.TryAdd(originalAddress, 0);
            _storageClearedAddresses.TryAdd(originalAddress, 0);
            return Task.CompletedTask;
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var hashHex = ToHex(codeHash);
            _code.TryGetValue(hashHex, out var code);
            return Task.FromResult(code);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            var hashHex = ToHex(codeHash);
            var snapshot = _activeSnapshot;
            if (snapshot != null)
            {
                _code.TryGetValue(hashHex, out var existing);
                snapshot.SaveCodeUndoIfNeeded(hashHex, existing?.ToArray());
            }
            _code[hashHex] = code;
            return Task.CompletedTask;
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            lock (_snapshotLock)
            {
                var snapshot = new CowStateSnapshot(
                    Interlocked.Increment(ref _nextSnapshotId),
                    new HashSet<string>(_dirtyAccounts.Keys),
                    CloneDirtyStorageSlots()
                );
                _activeSnapshot = snapshot;
                return Task.FromResult<IStateSnapshot>(snapshot);
            }
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is CowStateSnapshot cowSnapshot && _activeSnapshot == cowSnapshot)
            {
                _activeSnapshot = null;
            }
            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is CowStateSnapshot cowSnapshot)
            {
                lock (_snapshotLock)
                {
                    foreach (var kvp in cowSnapshot.AccountUndoLog)
                    {
                        if (kvp.Value != null)
                            _accounts[kvp.Key] = kvp.Value;
                        else
                            _accounts.TryRemove(kvp.Key, out _);
                    }

                    foreach (var address in cowSnapshot.ClearedStorageAddresses)
                    {
                        _storage.TryRemove(address, out _);
                    }

                    foreach (var addrKvp in cowSnapshot.StorageUndoLog)
                    {
                        foreach (var slotKvp in addrKvp.Value)
                        {
                            if (slotKvp.Value != null)
                            {
                                var accountStorage = _storage.GetOrAdd(addrKvp.Key,
                                    _ => new ConcurrentDictionary<BigInteger, byte[]>());
                                accountStorage[slotKvp.Key] = slotKvp.Value;
                            }
                            else
                            {
                                if (_storage.TryGetValue(addrKvp.Key, out var accountStorage))
                                    accountStorage.TryRemove(slotKvp.Key, out _);
                            }
                        }
                    }

                    foreach (var kvp in cowSnapshot.CodeUndoLog)
                    {
                        if (kvp.Value != null)
                            _code[kvp.Key] = kvp.Value;
                        else
                            _code.TryRemove(kvp.Key, out _);
                    }

                    _dirtyAccounts.Clear();
                    foreach (var addr in cowSnapshot.SnapshotDirtyAccounts)
                        _dirtyAccounts.TryAdd(addr, 0);

                    _dirtyStorageSlots.Clear();
                    foreach (var kvp in cowSnapshot.SnapshotDirtyStorageSlots)
                        _dirtyStorageSlots[kvp.Key] = new ConcurrentDictionary<BigInteger, byte>(
                            kvp.Value.Select(s => new KeyValuePair<BigInteger, byte>(s, 0)));

                    _activeSnapshot = null;
                }
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
        {
            return Task.FromResult<IReadOnlyCollection<string>>(_dirtyAccounts.Keys.ToList());
        }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            var key = OriginalAddress(address);
            if (!_dirtyStorageSlots.TryGetValue(key, out var dirtySlots))
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
            return Task.FromResult<IReadOnlyCollection<BigInteger>>(dirtySlots.Keys.ToList());
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
        {
            return Task.FromResult<IReadOnlyCollection<string>>(_storageClearedAddresses.Keys.ToList());
        }

        public Task ClearDirtyTrackingAsync()
        {
            _dirtyAccounts.Clear();
            _dirtyStorageSlots.Clear();
            _storageClearedAddresses.Clear();
            return Task.CompletedTask;
        }

        public void Clear()
        {
            _accounts.Clear();
            _storage.Clear();
            _code.Clear();
            _addressByAccountHash.Clear();
            _dirtyAccounts.Clear();
            _dirtyStorageSlots.Clear();
        }

        private void SaveStorageClearUndo(CowStateSnapshot snapshot, string normalizedAddress)
        {
            if (_storage.TryGetValue(normalizedAddress, out var currentStorage))
            {
                foreach (var kvp in currentStorage)
                {
                    snapshot.SaveStorageUndoIfNeeded(normalizedAddress, kvp.Key, kvp.Value?.ToArray());
                }
            }
            snapshot.MarkStorageCleared(normalizedAddress);
        }

        private Dictionary<string, HashSet<BigInteger>> CloneDirtyStorageSlots()
        {
            var clone = new Dictionary<string, HashSet<BigInteger>>();
            foreach (var kvp in _dirtyStorageSlots)
            {
                clone[kvp.Key] = new HashSet<BigInteger>(kvp.Value.Keys);
            }
            return clone;
        }

        private static bool IsAllZero(byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) return false;
            }
            return true;
        }

        private static string ToHex(byte[] bytes) => bytes?.ToHex();
    }

    internal class CowStateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public HashSet<string> SnapshotDirtyAccounts { get; }
        public Dictionary<string, HashSet<BigInteger>> SnapshotDirtyStorageSlots { get; }

        internal Dictionary<string, Account> AccountUndoLog { get; } = new();
        internal Dictionary<string, Dictionary<BigInteger, byte[]>> StorageUndoLog { get; } = new();
        internal Dictionary<string, byte[]> CodeUndoLog { get; } = new();
        internal HashSet<string> ClearedStorageAddresses { get; } = new();

        public CowStateSnapshot(
            int snapshotId,
            HashSet<string> dirtyAccounts,
            Dictionary<string, HashSet<BigInteger>> dirtyStorageSlots)
        {
            SnapshotId = snapshotId;
            SnapshotDirtyAccounts = dirtyAccounts;
            SnapshotDirtyStorageSlots = dirtyStorageSlots;
        }

        public void SaveAccountUndoIfNeeded(string address, Account original)
        {
            if (!AccountUndoLog.ContainsKey(address))
                AccountUndoLog[address] = original;
        }

        public void SaveStorageUndoIfNeeded(string address, BigInteger slot, byte[] original)
        {
            if (!StorageUndoLog.TryGetValue(address, out var slots))
            {
                slots = new Dictionary<BigInteger, byte[]>();
                StorageUndoLog[address] = slots;
            }
            if (!slots.ContainsKey(slot))
                slots[slot] = original;
        }

        public void SaveCodeUndoIfNeeded(string hashHex, byte[] original)
        {
            if (!CodeUndoLog.ContainsKey(hashHex))
                CodeUndoLog[hashHex] = original;
        }

        public void MarkStorageCleared(string address)
        {
            ClearedStorageAddresses.Add(address);
        }

        public void SetAccount(string address, Account account) { }
        public void SetStorage(string address, BigInteger slot, byte[] value) { }
        public void SetCode(byte[] codeHash, byte[] code) { }
        public void DeleteAccount(string address) { }
        public void ClearStorage(string address) { }

        public void Dispose() { }
    }
}
