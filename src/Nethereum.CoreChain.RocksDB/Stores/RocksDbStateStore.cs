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
            var account = _accountLayout.DecodeAccount(data);

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
            var data = _accountLayout.EncodeAccount(account);
            _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, key, data);

            if (_accountLayout.HasExternalCodeHash && account.CodeHash != null)
            {
                var chKey = GetCodeHashKey(key);
                _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, chKey, account.CodeHash);
            }

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

            var prefix = key;
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var storageKey = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(storageKey, prefix))
                    break;
                batch.Delete(storageKey, storageCf);
                iterator.Next();
            }

            _manager.Write(batch);
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

                if (hasExtCodeHash && key.Length != 20)
                {
                    iterator.Next();
                    continue;
                }

                var data = iterator.Value();
                var address = "0x" + key.ToHex();
                var account = _accountLayout.DecodeAccount(data);
                if (account != null)
                {
                    if (hasExtCodeHash)
                    {
                        var chKey = GetCodeHashKey(key);
                        account.CodeHash = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, chKey);
                    }

                    result[address.ToLowerInvariant()] = account;
                }

                iterator.Next();
            }

            return Task.FromResult(result);
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

            if (value == null || value.All(b => b == 0))
            {
                _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            }
            else
            {
                _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, value);
            }

            TrackStorageModification(key, address, slot);
            return Task.CompletedTask;
        }

        public Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address)
        {
            var result = new Dictionary<BigInteger, byte[]>();
            var prefix = GetAccountKey(address);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;

                var slotBytes = new byte[key.Length - prefix.Length];
                Buffer.BlockCopy(key, prefix.Length, slotBytes, 0, slotBytes.Length);
                var slot = new BigInteger(slotBytes, isUnsigned: true, isBigEndian: true);

                result[slot] = iterator.Value();
                iterator.Next();
            }

            return Task.FromResult(result);
        }

        public Task ClearStorageAsync(string address)
        {
            var prefix = GetAccountKey(address);

            using var batch = _manager.CreateWriteBatch();
            var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;
                batch.Delete(key, storageCf);
                iterator.Next();
            }

            _manager.Write(batch);
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
                    var data = _accountLayout.EncodeAccount(kvp.Value);
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

        private static byte[] GetAccountKey(string address)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
            return normalized.HexToByteArray();
        }

        // EIP-7864: code hash is stored in a separate trie leaf (sub-index 1) from
        // basic data (sub-index 0). The state store mirrors this by appending 0x01
        // to the 20-byte address key, keeping both in CF_STATE_ACCOUNTS.
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

        private static byte[] GetStorageKeyFromBytes(byte[] addressBytes, BigInteger slot)
        {
            var slotBytes = slot.ToByteArray(isUnsigned: true, isBigEndian: true).PadBytes(32);

            var key = new byte[addressBytes.Length + slotBytes.Length];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(slotBytes, 0, key, addressBytes.Length, slotBytes.Length);
            return key;
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

        public Task ClearDirtyTrackingAsync()
        {
            lock (_lock)
            {
                _dirtyAccounts.Clear();
                _dirtyStorageSlots.Clear();
            }
            return Task.CompletedTask;
        }
    }
}
