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

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbStateStore : IStateStore
    {
        private readonly RocksDbManager _manager;
        private readonly object _lock = new object();
        private int _nextSnapshotId = 0;
        private readonly Dictionary<int, RocksDbStateSnapshot> _activeSnapshots = new Dictionary<int, RocksDbStateSnapshot>();

        public RocksDbStateStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        public Task<Account> GetAccountAsync(string address)
        {
            var key = GetAccountKey(address);
            var data = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, key);
            var account = RocksDbSerializer.DeserializeAccount(data);
            return Task.FromResult(account);
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            var key = GetAccountKey(address);
            var data = RocksDbSerializer.SerializeAccount(account);
            _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, key, data);
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

            var prefix = key;
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var storageKey = iterator.Key();
                if (!StartsWith(storageKey, prefix))
                    break;
                batch.Delete(storageKey, storageCf);
                iterator.Next();
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var result = new Dictionary<string, Account>();

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var key = iterator.Key();
                var data = iterator.Value();

                var address = "0x" + key.ToHex();
                var account = RocksDbSerializer.DeserializeAccount(data);
                if (account != null)
                {
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

            TrackStorageModification(key);
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
                if (!StartsWith(key, prefix))
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
                if (!StartsWith(key, prefix))
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
                        if (!StartsWith(key, prefix))
                            break;
                        batch.Delete(key, storageCf);
                        iterator.Next();
                    }
                }

                foreach (var kvp in rocksSnapshot.PendingAccounts)
                {
                    var key = kvp.Key.HexToByteArray();
                    var data = RocksDbSerializer.SerializeAccount(kvp.Value);
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

                _manager.Write(batch);
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

                    _manager.Write(batch);
                    _activeSnapshots.Remove(rocksSnapshot.SnapshotId);
                }
            }

            return Task.CompletedTask;
        }

        private static byte[] GetAccountKey(string address)
        {
            var normalized = address?.ToLowerInvariant().Replace("0x", "") ?? "";
            return normalized.HexToByteArray();
        }

        private static byte[] GetStorageKey(string address, BigInteger slot)
        {
            var addressBytes = GetAccountKey(address);
            return GetStorageKeyFromBytes(addressBytes, slot);
        }

        private static byte[] GetStorageKeyFromBytes(byte[] addressBytes, BigInteger slot)
        {
            var slotBytes = slot.ToByteArray(isUnsigned: true, isBigEndian: true);
            var paddedSlot = new byte[32];
            if (slotBytes.Length <= 32)
            {
                Buffer.BlockCopy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
            }

            var key = new byte[addressBytes.Length + paddedSlot.Length];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(paddedSlot, 0, key, addressBytes.Length, paddedSlot.Length);
            return key;
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || prefix == null) return false;
            if (data.Length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i]) return false;
            }
            return true;
        }

        private void TrackAccountModification(string address)
        {
            lock (_lock)
            {
                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackAccountModification(address);
                }
            }
        }

        private void TrackStorageModification(byte[] storageKey)
        {
            lock (_lock)
            {
                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackStorageModification(storageKey);
                }
            }
        }
    }
}
