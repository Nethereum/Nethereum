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
using Nethereum.RLP;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbStateStore : IStateStore, IAddressHashCache, ISnapFlatStateWriter, IFlatStateBatchWriter, IDisposable
    {
        private readonly RocksDbManager _manager;
        private readonly object _lock = new object();
        private int _nextSnapshotId = 0;
        private readonly Dictionary<int, RocksDbStateSnapshot> _activeSnapshots = new Dictionary<int, RocksDbStateSnapshot>();
        private readonly HashSet<EvmAddress> _dirtyAccounts = new HashSet<EvmAddress>();
        private readonly Dictionary<EvmAddress, HashSet<BigInteger>> _dirtyStorageSlots = new Dictionary<EvmAddress, HashSet<BigInteger>>();
        private readonly HashSet<EvmAddress> _storageClearedAddresses = new HashSet<EvmAddress>();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<EvmAddress, byte[]> _addressHashCache = new();
        private bool _disposed;

        private readonly RocksDbSerializer _serializer;
        private readonly IAccountLayoutStrategy _accountLayout;

        private readonly bool _stateValuesVersioned;

        public RocksDbStateStore(
            RocksDbManager manager,
            RocksDbSerializer serializer = null,
            IAccountLayoutStrategy accountLayout = null,
            bool stateValuesVersioned = false)
        {
            _manager = manager;
            _serializer = serializer ?? RocksDbSerializer.Default;
            _accountLayout = accountLayout ?? RlpAccountLayout.Instance;
            _stateValuesVersioned = stateValuesVersioned;
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

        public bool TryGetAddressHash(EvmAddress address, out byte[] hash) => _addressHashCache.TryGetValue(address, out hash);

        public void SetAddressHash(EvmAddress address, byte[] hash) => _addressHashCache[address] = hash;

        public void ClearAddressHashCache() => _addressHashCache.Clear();

        public Task<Account> GetAccountAsync(string address)
            => Task.FromResult(ReadAccountByFlatKey(GetAccountKey(address)));

        public Task<Account> GetAccountAsync(EvmAddress address)
            => Task.FromResult(ReadAccountByFlatKey(GetAccountKeyBytes(address)));

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => Task.FromResult(ReadAccountByFlatKey(accountHash));

        private Account ReadAccountByFlatKey(byte[] key)
        {
            var data = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, key);
            var account = DecodeAccountValue(data);

            if (account != null && _accountLayout.HasExternalCodeHash)
                account.CodeHash = _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, GetCodeHashKey(key));

            return account;
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

        public Task SaveAccountAsync(EvmAddress address, Account account)
        {
            var key = GetAccountKeyBytes(address);
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

        public Task<bool> AccountExistsAsync(EvmAddress address)
        {
            var key = GetAccountKeyBytes(address);
            var exists = _manager.KeyExists(RocksDbManager.CF_STATE_ACCOUNTS, key);
            return Task.FromResult(exists);
        }

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
        {
            SaveAccountByHash(accountHash, account);
            return Task.CompletedTask;
        }

        internal void SaveAccountByHash(byte[] accountHash, Account account)
        {
            _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, accountHash, EncodeFlatAccountValueByHash(account));
            if (_accountLayout.HasExternalCodeHash && account.CodeHash != null)
                _manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, GetCodeHashKey(accountHash), account.CodeHash);
        }

        internal byte[] EncodeFlatAccountValueByHash(Account account)
        {
            var encoded = _accountLayout.EncodeAccount(account);
            var data = new byte[20 + encoded.Length];
            Buffer.BlockCopy(encoded, 0, data, 20, encoded.Length);
            return _stateValuesVersioned ? StateValueEnvelope.Encode(data) : data;
        }

        internal byte[] GetCodeHashRowKey(byte[] accountHash) => GetCodeHashKey(accountHash);

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
        {
            SaveStorageByHash(accountHash, slotKeccak, value);
            return Task.CompletedTask;
        }

        internal void SaveStorageByHash(byte[] accountHash, byte[] slotKeccak, byte[] value)
        {
            if (accountHash == null || accountHash.Length != 32)
                throw new ArgumentException("accountHash must be 32 bytes", nameof(accountHash));
            if (slotKeccak == null || slotKeccak.Length != 32)
                throw new ArgumentException("slotKeccak must be 32 bytes", nameof(slotKeccak));
            var key = FlatStateWalk.OwnerSlotKey(accountHash, slotKeccak);
            if (SnapFlatStorageValue.ClearsSlot(value)) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, TrimStorageValue(value));
        }

        internal Account DecodeFlatAccountValue(byte[] data) => DecodeAccountValue(data);

        internal bool HasExternalCodeHashLayout => _accountLayout.HasExternalCodeHash;

        internal bool HasRlpAccountLayout => _accountLayout is RlpAccountLayout;

        internal byte[] GetFlatCodeHash(byte[] accountHash)
            => _manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, GetCodeHashKey(accountHash));

        internal void DeleteFlatAccountByHash(byte[] accountHash)
        {
            _manager.Delete(RocksDbManager.CF_STATE_ACCOUNTS, accountHash);
            _manager.Delete(RocksDbManager.CF_STATE_ACCOUNTS, GetCodeHashKey(accountHash));
        }

        public Task DeleteAccountByHashAsync(byte[] accountHash)
        {
            DeleteFlatAccountByHash(accountHash);
            return Task.CompletedTask;
        }

        public Task DeleteAccountAsync(string address) => DeleteAccountAsync(EvmAddress.FromHex(address));

        public Task DeleteAccountAsync(EvmAddress address)
        {
            using var batch = _manager.CreateWriteBatch();
            var accountsCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            AddAccountDeleteToBatchByKey(batch, accountsCf, storageCf, GetAccountKeyBytes(address));
            _manager.Write(batch);
            TrackAccountModification(address);
            return Task.CompletedTask;
        }

        private void AddAccountDeleteToBatchByKey(WriteBatch batch, ColumnFamilyHandle accountsCf, ColumnFamilyHandle storageCf, byte[] key)
        {
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
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var result = new Dictionary<string, Account>();
            foreach (var entry in EnumerateAccounts()) result[entry.Key] = entry.Value;
            return Task.FromResult(result);
        }

#pragma warning disable CS1998
        public async System.Collections.Generic.IAsyncEnumerable<System.Collections.Generic.KeyValuePair<string, Account>> StreamAccountsAsync()
#pragma warning restore CS1998
        {
            foreach (var entry in EnumerateAccounts()) yield return entry;
        }

        private IEnumerable<KeyValuePair<string, Account>> EnumerateAccounts()
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
                    yield return new KeyValuePair<string, Account>(address, account);
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

        public Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot)
        {
            var key = GetStorageKeyFromBytes(GetAccountKeyBytes(address), slot);
            var data = _manager.Get(RocksDbManager.CF_STATE_STORAGE, key);
            return Task.FromResult(data);
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            var key = GetStorageKey(address, slot);
            if (SnapFlatStorageValue.ClearsSlot(value)) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, TrimStorageValue(value));
            TrackStorageModification(key, address, slot);
            return Task.CompletedTask;
        }

        public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value)
        {
            var key = GetStorageKeyFromBytes(GetAccountKeyBytes(address), slot);
            if (SnapFlatStorageValue.ClearsSlot(value)) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, TrimStorageValue(value));
            TrackStorageModification(key, address, slot);
            return Task.CompletedTask;
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
        {
            if (slotKeccak == null || slotKeccak.Length != 32)
                throw new ArgumentException("slotKeccak must be 32 bytes (keccak(slot))", nameof(slotKeccak));

            var addressBytes = GetAccountKey(address);
            var key = new byte[addressBytes.Length + 32];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(slotKeccak, 0, key, addressBytes.Length, 32);

            if (SnapFlatStorageValue.ClearsSlot(value)) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, TrimStorageValue(value));
            return Task.CompletedTask;
        }

        public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value)
        {
            if (slotKeccak == null || slotKeccak.Length != 32)
                throw new ArgumentException("slotKeccak must be 32 bytes (keccak(slot))", nameof(slotKeccak));

            var addressBytes = GetAccountKeyBytes(address);
            var key = new byte[addressBytes.Length + 32];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(slotKeccak, 0, key, addressBytes.Length, 32);

            if (SnapFlatStorageValue.ClearsSlot(value)) _manager.Delete(RocksDbManager.CF_STATE_STORAGE, key);
            else _manager.Put(RocksDbManager.CF_STATE_STORAGE, key, TrimStorageValue(value));
            return Task.CompletedTask;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
            => GetAllStorageByPrefixAsync(GetAccountKey(address));

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address)
            => GetAllStorageByPrefixAsync(GetAccountKeyBytes(address));

        private Task<Dictionary<byte[], byte[]>> GetAllStorageByPrefixAsync(byte[] prefix)
        {
            var result = new Dictionary<byte[], byte[]>(Nethereum.Util.ByteArrayComparer.Current);

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

        public Task ClearStorageAsync(string address) => ClearStorageAsync(EvmAddress.FromHex(address));

        public Task ClearStorageAsync(EvmAddress address)
        {
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            AddStorageClearToBatchByKey(batch, cf, GetAccountKeyBytes(address));
            _manager.Write(batch);
            lock (_lock)
            {
                _storageClearedAddresses.Add(address);
            }
            return Task.CompletedTask;
        }

        private void AddStorageClearToBatchByKey(WriteBatch batch, ColumnFamilyHandle storageCf, byte[] prefix)
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            iterator.Seek(prefix);
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix)) break;
                batch.Delete(key, storageCf);
                iterator.Next();
            }
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash) => Task.FromResult(GetCode(codeHash));

        internal byte[] GetCode(byte[] codeHash)
            => codeHash == null ? null : _manager.Get(RocksDbManager.CF_STATE_CODE, codeHash);

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            if (codeHash == null) return Task.CompletedTask;
            _manager.Put(RocksDbManager.CF_STATE_CODE, codeHash, code);
            TrackCodeModification(codeHash);
            return Task.CompletedTask;
        }

        private void AddAccountPutToBatch(WriteBatch batch, ColumnFamilyHandle accountsCf, EvmAddress address, Account account)
        {
            var key = GetAccountKeyBytes(address);
            var data = EncodeAccountValue(address, account);
            batch.Put(key, data, accountsCf);
            if (_accountLayout.HasExternalCodeHash && account.CodeHash != null)
                batch.Put(GetCodeHashKey(key), account.CodeHash, accountsCf);
        }

        private void AddStorageWriteToBatch(WriteBatch batch, ColumnFamilyHandle storageCf, EvmAddress address, BigInteger slot, byte[] value)
        {
            var key = GetStorageKeyFromBytes(GetAccountKeyBytes(address), slot);
            bool isZero = value == null || value.All(b => b == 0);
            if (isZero) batch.Delete(key, storageCf);
            else batch.Put(key, TrimStorageValue(value), storageCf);
        }

        private static byte[] TrimStorageValue(byte[] value) => value.TrimZeroBytes();

        internal void AddBatchToWriteBatch(WriteBatch writeBatch, FlatStateBatch batch)
        {
            var accountsCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            var storageCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            var codeCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_CODE);

            foreach (var address in batch.DeletedAccountEvmAddresses)
                AddAccountDeleteToBatchByKey(writeBatch, accountsCf, storageCf, GetAccountKeyBytes(address));

            foreach (var address in batch.ClearedStorageEvmAddresses)
                AddStorageClearToBatchByKey(writeBatch, storageCf, GetAccountKeyBytes(address));

            foreach (var entry in batch.NonZeroStorageEvm)
                AddStorageWriteToBatch(writeBatch, storageCf, entry.Address, entry.Slot, entry.Value);

            foreach (var entry in batch.DeletedSlotsEvm)
                AddStorageWriteToBatch(writeBatch, storageCf, entry.Address, entry.Slot, Array.Empty<byte>());

            foreach (var entry in batch.AccountsEvm)
                AddAccountPutToBatch(writeBatch, accountsCf, entry.Address, entry.Account);

            foreach (var entry in batch.Code)
                writeBatch.Put(entry.CodeHash, entry.Code, codeCf);
        }

        public Task ApplyBatchAsync(FlatStateBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            using var writeBatch = _manager.CreateWriteBatch();
            AddBatchToWriteBatch(writeBatch, batch);

            _manager.Write(writeBatch);

            var cleared = batch.ClearedStorageEvmAddresses;
            foreach (var address in batch.DeletedAccountEvmAddresses)
                TrackAccountModification(address);
            if (cleared.Count > 0)
            {
                lock (_lock)
                {
                    foreach (var address in cleared)
                        _storageClearedAddresses.Add(address);
                }
            }
            foreach (var entry in batch.NonZeroStorageEvm)
                TrackStorageModification(GetStorageKeyFromBytes(GetAccountKeyBytes(entry.Address), entry.Slot), entry.Address, entry.Slot);
            foreach (var entry in batch.DeletedSlotsEvm)
                TrackStorageModification(GetStorageKeyFromBytes(GetAccountKeyBytes(entry.Address), entry.Slot), entry.Address, entry.Slot);
            foreach (var entry in batch.AccountsEvm)
                TrackAccountModification(entry.Address);
            foreach (var entry in batch.Code)
                TrackCodeModification(entry.CodeHash);

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
                            batch.Put(key, TrimStorageValue(slotKvp.Value), storageCf);
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

        private byte[] GetAccountKey(string address) => GetAccountKeyBytes(EvmAddress.FromHex(address));

        private byte[] GetAccountKeyBytes(EvmAddress address)
        {
            if (_addressHashCache.TryGetValue(address, out var cached)) return cached;
            var key = StateKeys.AccountKey(address);
            _addressHashCache[address] = key;
            return key;
        }

        private static byte[] GetCodeHashKey(byte[] accountKey)
        {
            var chKey = new byte[accountKey.Length + 1];
            Buffer.BlockCopy(accountKey, 0, chKey, 0, accountKey.Length);
            chKey[accountKey.Length] = 0x01;
            return chKey;
        }

        private byte[] GetStorageKey(string address, BigInteger slot)
        {
            var addressBytes = GetAccountKey(address);
            return GetStorageKeyFromBytes(addressBytes, slot);
        }

        private static byte[] GetStorageKeyFromBytes(byte[] addressBytes, BigInteger slot)
        {
            var slotHash = StateKeys.StorageSlotKey(slot);

            var key = new byte[addressBytes.Length + slotHash.Length];
            Buffer.BlockCopy(addressBytes, 0, key, 0, addressBytes.Length);
            Buffer.BlockCopy(slotHash, 0, key, addressBytes.Length, slotHash.Length);
            return key;
        }

        private byte[] EncodeAccountValue(string address, Account account)
            => EncodeAccountValue(EvmAddress.FromHex(address), account);

        private byte[] EncodeAccountValue(EvmAddress address, Account account)
        {
            var encoded = _accountLayout.EncodeAccount(account);
            var addressBytes = address.ToByteArray();
            var value = new byte[20 + encoded.Length];
            Buffer.BlockCopy(addressBytes, 0, value, 0, 20);
            Buffer.BlockCopy(encoded, 0, value, 20, encoded.Length);
            return _stateValuesVersioned ? StateValueEnvelope.Encode(value) : value;
        }

        private Account DecodeAccountValue(byte[] data)
        {
            return DecodeAccountValue(data, out _);
        }

        private Account DecodeAccountValue(byte[] data, out byte[] inlineAddress)
        {
            inlineAddress = null;
            if (data == null) return null;

            ReadOnlySpan<byte> payload = _stateValuesVersioned
                ? StateValueEnvelope.Decode(data, out _)
                : data;

            if (payload.Length < 20) return null;
            inlineAddress = payload.Slice(0, 20).ToArray();
            var encoded = payload.Slice(20).ToArray();
            return _accountLayout.DecodeAccount(encoded);
        }

        private void TrackAccountModification(string address) => TrackAccountModification(EvmAddress.FromHex(address));

        private void TrackAccountModification(EvmAddress address)
        {
            lock (_lock)
            {
                _dirtyAccounts.Add(address);
                if (_activeSnapshots.Count == 0) return;
                foreach (var snapshot in _activeSnapshots.Values)
                {
                    snapshot.TrackAccountModification(address);
                }
            }
        }

        private void TrackStorageModification(byte[] storageKey, string address, BigInteger slot)
            => TrackDirtyStorage(storageKey, EvmAddress.FromHex(address), slot);

        private void TrackStorageModification(byte[] storageKey, EvmAddress address, BigInteger slot)
            => TrackDirtyStorage(storageKey, address, slot);

        private void TrackDirtyStorage(byte[] storageKey, EvmAddress normalizedAddress, BigInteger slot)
        {
            lock (_lock)
            {
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
                return Task.FromResult<IReadOnlyCollection<string>>(HexOf(_dirtyAccounts));
            }
        }

        public Task<IReadOnlyCollection<EvmAddress>> GetDirtyAccountsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<EvmAddress>>(_dirtyAccounts.ToList());
            }
        }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => GetDirtyStorageSlotsAsync(EvmAddress.FromHex(address));

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(EvmAddress address)
        {
            lock (_lock)
            {
                if (!_dirtyStorageSlots.TryGetValue(address, out var dirtySlots))
                    return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(dirtySlots.ToList());
            }
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<string>>(HexOf(_storageClearedAddresses));
            }
        }

        public Task<IReadOnlyCollection<EvmAddress>> GetStorageClearedAccountsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyCollection<EvmAddress>>(_storageClearedAddresses.ToList());
            }
        }

        private static List<string> HexOf(IEnumerable<EvmAddress> addresses)
        {
            var hex = new List<string>();
            foreach (var address in addresses) hex.Add(address.ToHexLower());
            return hex;
        }

        public Task ClearDirtyTrackingAsync()
        {
            lock (_lock)
            {
                _dirtyAccounts.Clear();
                _dirtyStorageSlots.Clear();
                _storageClearedAddresses.Clear();
            }
            _addressHashCache.Clear();
            return Task.CompletedTask;
        }
    }
}
