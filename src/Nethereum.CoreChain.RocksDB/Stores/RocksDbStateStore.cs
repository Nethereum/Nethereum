using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.RocksDB.Snapshots;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbStateStore : IStateStore, IDisposable
    {
        private readonly RocksDbManager _manager;
        private readonly object _lock = new object();
        private int _nextSnapshotId = 0;
        private readonly Dictionary<int, RocksDbStateSnapshot> _activeSnapshots = new Dictionary<int, RocksDbStateSnapshot>();
        private readonly HashSet<string> _dirtyAccounts = new HashSet<string>();
        private readonly Dictionary<string, HashSet<BigInteger>> _dirtyStorageSlots = new Dictionary<string, HashSet<BigInteger>>();
        private readonly HashSet<string> _storageClearedAddresses = new HashSet<string>();
        private bool _disposed;

        private readonly RocksDbSerializer _serializer;
        private readonly IAccountLayoutStrategy _accountLayout;

        public RocksDbStateStore(
            RocksDbManager manager,
            RocksDbSerializer serializer = null,
            IAccountLayoutStrategy accountLayout = null)
        {
            _manager = manager;
            _serializer = serializer ?? RocksDbSerializer.Default;
            _accountLayout = accountLayout ?? RlpAccountLayout.Instance;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_lock)
                    {
                        foreach (var snapshot in _activeSnapshots.Values)
                        {
                            snapshot.Dispose();
                        }
                        _activeSnapshots.Clear();
                    }
                }
                _disposed = true;
            }
        }

        public Task<Account> GetAccountAsync(string address)
        {
            var key = GetAccountKey(address);
            var data = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, key);
            var account = DecodeAccountValue(data);

            if (account != null && _accountLayout.HasExternalCodeHash)
            {
                var chKey = GetCodeHashKey(key);
                account.CodeHash = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, chKey);
            }

            return Task.FromResult(account);
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            var key = GetAccountKey(address);
            var data = EncodeAccountValue(address, account);
            _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, key, data);
            if (_accountLayout.HasExternalCodeHash && account.CodeHash != null)
                _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, GetCodeHashKey(key), account.CodeHash);
            TrackAccountModification(address);
            return Task.CompletedTask;
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            var key = GetAccountKey(address);
            var exists = _manager.KeyExists(RocksDbManager.CF_STATE_ACCOUNTS, key);
            return Task.FromResult(exists);
        }

        public Task DeleteAccountAsync(string address)
        {
            var key = GetAccountKey(address);
            using var batch = _manager.CreateWriteBatch();
            var accountsCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            batch.Delete(key, accountsCf);
            if (_accountLayout.HasExternalCodeHash)
                batch.Delete(GetCodeHashKey(key), accountsCf);
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(key);
            while (iterator.Valid())
            {
                var storageKey = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(storageKey, key)) break;
                batch.Delete(storageKey, storageCf);
                iterator.Next();
            }
            _manager.Write(batch);
            // Track for IncrementalStateRootCalculator (mainnet block 51,921
            // SELFDESTRUCT-cleanup fix — without this the in-memory trie
            // retained the leaf after the on-disk row was deleted).
            TrackAccountModification(address);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var hasExtCodeHash = _accountLayout.HasExternalCodeHash;
            var result = new Dictionary<string, Account>();

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var key = iterator.Key();

                // Account leaves are keccak(addr) (32 bytes); code-hash
                // sub-leaves are keccak(addr) || 0x01 (33 bytes).
                if (key.Length != 32)
                {
                    iterator.Next();
                    continue;
                }

                var data = iterator.Value();
                var account = DecodeAccountValue(data, out var inlineAddress);
                if (account != null && inlineAddress != null)
                {
                    if (hasExtCodeHash)
                    {
                        var chKey = GetCodeHashKey(key);
                        account.CodeHash = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, chKey);
                    }

                    var address = "0x" + inlineAddress.ToHex();
                    result[address] = account;
                }

                iterator.Next();
            }

            return Task.FromResult(result);
        }

#pragma warning disable CS1998 // yield-only async iterator does not need await
        public async System.Collections.Generic.IAsyncEnumerable<System.Collections.Generic.KeyValuePair<string, Account>> StreamAccountsAsync()
#pragma warning restore CS1998
        {
            var hasExtCodeHash = _accountLayout.HasExternalCodeHash;
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length != 32)
                {
                    iterator.Next();
                    continue;
                }
                var data = iterator.Value();
                var account = DecodeAccountValue(data, out var inlineAddress);
                if (account != null && inlineAddress != null)
                {
                    if (hasExtCodeHash)
                    {
                        var chKey = GetCodeHashKey(key);
                        account.CodeHash = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, chKey);
                    }
                    var address = "0x" + inlineAddress.ToHex();
                    yield return new System.Collections.Generic.KeyValuePair<string, Account>(address, account);
                }
                iterator.Next();
            }
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var key = GetStorageKey(address, slot);
            var data = _manager.Get(RocksDbManager.CF_STATE_STORAGE, key);
            return Task.FromResult(data);
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            var key = GetStorageKey(address, slot);
            bool isZero = value == null || value.All(b => b == 0);
            if (isZero) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, value);
            TrackStorageModification(key, address, slot);
            return Task.CompletedTask;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
        {
            var result = new Dictionary<byte[], byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            var prefix = GetAccountKey(address);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;

                var slotHash = new byte[key.Length - prefix.Length];
                Buffer.BlockCopy(key, prefix.Length, slotHash, 0, slotHash.Length);

                result[slotHash] = iterator.Value();
                iterator.Next();
            }

            return Task.FromResult(result);
        }

        public Task ClearStorageAsync(string address)
        {
            var prefix = GetAccountKey(address);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix)) break;
                batch.Delete(key, cf);
                iterator.Next();
            }
            _manager.Write(batch);
            // Signal IncrementalStateRootCalculator: drop any cached storage
            // trie for this address. Without this, SELFDESTRUCT + same-block
            // re-materialisation (pre-EIP-158) keeps the stale trie and
            // corrupts the leaf's storageRoot.
            lock (_lock)
            {
                var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
                _storageClearedAddresses.Add(normalizedAddress);
            }
            return Task.CompletedTask;
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            if (codeHash == null) return Task.FromResult<byte[]>(null);
            var data = _manager.Get(RocksDbManager.CF_STATE_CODE, codeHash);
            return Task.FromResult(data);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            if (codeHash == null) return Task.CompletedTask;
            _manager.Put(RocksDbManager.CF_STATE_CODE, codeHash, code);
            TrackCodeModification(codeHash);
            return Task.CompletedTask;
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            lock (_lock)
            {
                var snapshotId = _nextSnapshotId++;
                var snapshot = new RocksDbStateSnapshot(_manager, snapshotId);
                _activeSnapshots[snapshotId] = snapshot;
                return Task.FromResult<IStateSnapshot>(snapshot);
            }
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is RocksDbStateSnapshot rocksSnapshot)
            {
                using var batch = _manager.CreateWriteBatch();
                var accountsCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
                var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
                var codeCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_CODE);

                foreach (var deleted in rocksSnapshot.DeletedAccounts)
                {
                    var key = deleted.HexToByteArray();
                    batch.Delete(key, accountsCf);
                }

                foreach (var cleared in rocksSnapshot.ClearedStorage)
                {
                    var prefix = cleared.HexToByteArray();
                    using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE, rocksSnapshot.SnapshotReadOptions);
                    iterator.Seek(prefix);

                    while (iterator.Valid())
                    {
                        var key = iterator.Key();
                        if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                            break;
                        batch.Delete(key, storageCf);
                        iterator.Next();
                    }
                }

                foreach (var kvp in rocksSnapshot.PendingAccounts)
                {
                    var key = kvp.Key.HexToByteArray();
                    rocksSnapshot.OriginalAddresses.TryGetValue(kvp.Key, out var originalAddr);
                    var data = EncodeAccountValue(originalAddr, kvp.Value);
                    batch.Put(key, data, accountsCf);
                }

                foreach (var addressKvp in rocksSnapshot.PendingStorage)
                {
                    var addressBytes = addressKvp.Key.HexToByteArray();
                    foreach (var slotKvp in addressKvp.Value)
                    {
                        var key = GetStorageKeyFromBytes(addressBytes, slotKvp.Key);
                        if (slotKvp.Value == null || slotKvp.Value.All(b => b == 0))
                        {
                            batch.Delete(key, storageCf);
                        }
                        else
                        {
                            batch.Put(key, slotKvp.Value, storageCf);
                        }
                    }
                }

                foreach (var kvp in rocksSnapshot.PendingCode)
                {
                    var key = kvp.Key.HexToByteArray();
                    batch.Put(key, kvp.Value, codeCf);
                }

                lock (_lock)
                {
                    _manager.Write(batch);
                    _activeSnapshots.Remove(rocksSnapshot.SnapshotId);
                }
                rocksSnapshot.Dispose();
            }

            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            if (snapshot is RocksDbStateSnapshot rocksSnapshot)
            {
                lock (_lock)
                {
                    using var batch = _manager.CreateWriteBatch();
                    var accountsCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
                    var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
                    var codeCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_CODE);

                    foreach (var address in rocksSnapshot.ModifiedAddresses)
                    {
                        var key = address.HexToByteArray();
                        var originalData = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, key, rocksSnapshot.SnapshotReadOptions);

                        if (originalData != null)
                        {
                            batch.Put(key, originalData, accountsCf);
                        }
                        else
                        {
                            batch.Delete(key, accountsCf);
                        }
                    }

                    foreach (var storageKey in rocksSnapshot.ModifiedStorageKeys)
                    {
                        var originalData = _manager.Get(RocksDbManager.CF_STATE_STORAGE, storageKey, rocksSnapshot.SnapshotReadOptions);

                        if (originalData != null)
                        {
                            batch.Put(storageKey, originalData, storageCf);
                        }
                        else
                        {
                            batch.Delete(storageKey, storageCf);
                        }
                    }

                    foreach (var codeHash in rocksSnapshot.ModifiedCodeHashes)
                    {
                        var originalData = _manager.Get(RocksDbManager.CF_STATE_CODE, codeHash, rocksSnapshot.SnapshotReadOptions);

                        if (originalData != null)
                        {
                            batch.Put(codeHash, originalData, codeCf);
                        }
                        else
                        {
                            batch.Delete(codeHash, codeCf);
                        }
                    }

                    _manager.Write(batch);
                    _activeSnapshots.Remove(rocksSnapshot.SnapshotId);
                }
                rocksSnapshot.Dispose();
            }

            return Task.CompletedTask;
        }

        // Yellow Paper §4.1: world-state trie maps keccak256(address) → account RLP.
        // Account keys are derived via StateKeys so the cache key equals the trie key.
        private static byte[] GetAccountKey(string address) => StateKeys.AccountKey(address);

        // EIP-7864: code hash is stored in a separate trie leaf (sub-index 1) from
        // basic data (sub-index 0). The state store mirrors this by appending 0x01
        // to the 32-byte account key, keeping both in CF_STATE_ACCOUNTS.
        private static byte[] GetCodeHashKey(byte[] accountKey)
        {
            var chKey = new byte[accountKey.Length + 1];
            Buffer.BlockCopy(accountKey, 0, chKey, 0, accountKey.Length);
            chKey[accountKey.Length] = 0x01;
            return chKey;
        }

        private static byte[] GetStorageKey(string address, BigInteger slot)
        {
            var addressBytes = GetAccountKey(address);
            return GetStorageKeyFromBytes(addressBytes, slot);
        }

        // Composite key: keccak(addr) (32 bytes) || keccak(slot) (32 bytes) = 64 bytes.
        // Yellow Paper §4.1 storage-trie path. Aligns with geth/erigon/reth and
        // matches the snap/1 wire shape natively (no on-read conversion). Recovery
        // of the original slot from the on-disk key is intentionally one-way; the
        // dirty-storage tracking cache holds BigInteger slots for callers that need
        // them.
        private static byte[] GetStorageKeyFromBytes(byte[] addressBytes, BigInteger slot)
        {
            var slotHash = StateKeys.StorageSlotKey(slot);

            var key = new byte[addressBytes.Length + slotHash.Length];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(slotHash, 0, key, addressBytes.Length, slotHash.Length);
            return key;
        }

        // Account value layout: address[20] ‖ accountLayout.Encode(account).
        // The inline address is what GetAllAccountsAsync / StreamAccountsAsync
        // yield to callers, since the key is now keccak(addr) and no longer
        // reversible to the original address.
        private byte[] EncodeAccountValue(string address, Account account)
        {
            var encoded = _accountLayout.EncodeAccount(account);
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
            var value = new byte[20 + encoded.Length];
            Buffer.BlockCopy(addressBytes, 0, value, 0, 20);
            Buffer.BlockCopy(encoded, 0, value, 20, encoded.Length);
            return value;
        }

        private Account DecodeAccountValue(byte[] data)
        {
            return DecodeAccountValue(data, out _);
        }

        private Account DecodeAccountValue(byte[] data, out byte[] inlineAddress)
        {
            inlineAddress = null;
            if (data == null || data.Length < 20) return null;
            inlineAddress = new byte[20];
            Buffer.BlockCopy(data, 0, inlineAddress, 0, 20);
            var encoded = new byte[data.Length - 20];
            Buffer.BlockCopy(data, 20, encoded, 0, encoded.Length);
            return _accountLayout.DecodeAccount(encoded);
        }

        private void TrackAccountModification(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
                _dirtyAccounts.Add(normalizedAddress);
                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackAccountModification(address);
                }
            }
        }

        private void TrackStorageModification(byte[] storageKey, string address, BigInteger slot)
        {
            lock (_lock)
            {
                var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
                _dirtyAccounts.Add(normalizedAddress);

                if (!_dirtyStorageSlots.TryGetValue(normalizedAddress, out var dirtySlots))
                {
                    dirtySlots = new HashSet<BigInteger>();
                    _dirtyStorageSlots[normalizedAddress] = dirtySlots;
                }
                dirtySlots.Add(slot);

                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackStorageModification(storageKey);
                }
            }
        }

        private void TrackCodeModification(byte[] codeHash)
        {
            lock (_lock)
            {
                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackCodeModification(codeHash);
                }
            }
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<string>>(_dirtyAccounts.ToList());
            }
        }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            lock (_lock)
            {
                var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
                if (!_dirtyStorageSlots.TryGetValue(normalizedAddress, out var dirtySlots))
                    return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(dirtySlots.ToList());
            }
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<string>>(_storageClearedAddresses.ToList());
            }
        }

        public Task ClearDirtyTrackingAsync()
        {
            lock (_lock)
            {
                _dirtyAccounts.Clear();
                _dirtyStorageSlots.Clear();
                _storageClearedAddresses.Clear();
            }
            return Task.CompletedTask;
        }
    }
}
