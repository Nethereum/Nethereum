using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class BufferedFlatStateStore : IStateStore, IStateWriteBuffer, ISnapFlatStateWriter, IFlatCacheInvalidatable, IStateReadStats, IStateReadMeterHost, IDirtyKeySource, IAddressHashCache, IBaseAbsenceSource
    {
        private readonly IStateStore _inner;
        private readonly StateOverlay _overlay = new();
        private readonly FlatStateCache _cache;
        private readonly IPendingFlushFlatOverlay _pendingFlush;
        private bool _buffering;
        private volatile StateReadMeter _meter;

        public bool TryAttachReadMeter(StateReadMeter meter)
        {
            _meter = meter;
            return true;
        }

        public void DetachReadMeter(StateReadMeter meter)
        {
            if (ReferenceEquals(_meter, meter)) _meter = null;
        }

        private long _accountReads;
        private long _storageReads;
        public long AccountReads => System.Threading.Interlocked.Read(ref _accountReads);
        public long StorageReads => System.Threading.Interlocked.Read(ref _storageReads);

        public BufferedFlatStateStore(IStateStore inner, FlatStateCache cache = null, IPendingFlushFlatOverlay pendingFlush = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache;
            _pendingFlush = pendingFlush;
        }


        public bool TryAttachDirtyKeyObserver(IDirtyKeyObserver observer) => _overlay.TryAttachObserver(observer);

        public void DetachDirtyKeyObserver(IDirtyKeyObserver observer) => _overlay.DetachObserver(observer);

        private ISnapFlatStateWriter InnerFlatWriter
            => _inner as ISnapFlatStateWriter
               ?? throw new NotSupportedException(
                   $"{_inner.GetType().Name} is not an ISnapFlatStateWriter; snap-flat rows written through BufferedFlatStateStore would be silently discarded.");

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => InnerFlatWriter.GetAccountByHashAsync(accountHash);

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            => InnerFlatWriter.SaveAccountByHashAsync(accountHash, account);

        public Task DeleteAccountByHashAsync(byte[] accountHash)
            => InnerFlatWriter.DeleteAccountByHashAsync(accountHash);

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            => InnerFlatWriter.SaveStorageByHashAsync(accountHash, slotKeccak, value);

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);

        public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value)
            => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);

        public bool TryGetAddressHash(EvmAddress address, out byte[] hash)
        {
            if (_inner is IAddressHashCache cache) return cache.TryGetAddressHash(address, out hash);
            hash = null;
            return false;
        }

        public void SetAddressHash(EvmAddress address, byte[] hash) => (_inner as IAddressHashCache)?.SetAddressHash(address, hash);

        public void ClearAddressHashCache() => (_inner as IAddressHashCache)?.ClearAddressHashCache();


        public Task<Account> GetAccountAsync(string address) => GetAccountAsync(EvmAddress.FromHex(address));

        public async Task<Account> GetAccountAsync(EvmAddress address)
        {
            var meter = _meter;
            var start = meter == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            if (!StateReadMeter.ReadsSuppressed) System.Threading.Interlocked.Increment(ref _accountReads);
            Account result;
            if (_overlay.TryGetAccount(address, out var account)) result = account;
            else if (_pendingFlush != null && _pendingFlush.TryGetAccount(address, out var pending, out var pendingDeleted))
                result = pendingDeleted ? null : pending;
            else if (_cache != null && _cache.TryGetAccount(address, out var cached))
                result = ReferenceEquals(cached, FlatStateCache.AccountTombstone) ? null : cached;
            else
            {
                result = await _inner.GetAccountAsync(address).ConfigureAwait(false);
                if (result == null && _buffering) _overlay.NoteAbsentInBase(address);
            }
            meter?.AddAccountRead(System.Diagnostics.Stopwatch.GetTimestamp() - start, address);
            return result;
        }

        public bool WasAbsentInBase(EvmAddress address) => _buffering && _overlay.IsAbsentInBase(address);

        public Task SaveAccountAsync(string address, Account account) => SaveAccountAsync(EvmAddress.FromHex(address), account);

        public Task SaveAccountAsync(EvmAddress address, Account account)
        {
            if (!_buffering) return _inner.SaveAccountAsync(address, account);
            _overlay.SaveAccount(address, account);
            return Task.CompletedTask;
        }

        public Task<bool> AccountExistsAsync(string address) => AccountExistsAsync(EvmAddress.FromHex(address));

        public async Task<bool> AccountExistsAsync(EvmAddress address)
        {
            var meter = _meter;
            if (meter == null) return await _overlay.AccountExistsAsync(_inner, address).ConfigureAwait(false);
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            var exists = await _overlay.AccountExistsAsync(_inner, address).ConfigureAwait(false);
            meter.AddAccountRead(System.Diagnostics.Stopwatch.GetTimestamp() - start, address);
            return exists;
        }

        public Task DeleteAccountAsync(string address) => DeleteAccountAsync(EvmAddress.FromHex(address));

        public Task DeleteAccountAsync(EvmAddress address)
        {
            if (!_buffering) return _inner.DeleteAccountAsync(address);
            _overlay.DeleteAccount(address);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
            => _overlay.MergeAllAccountsAsync(_inner);

        public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
            => _overlay.StreamMergedAccountsAsync(_inner);

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => GetStorageAsync(EvmAddress.FromHex(address), slot);

        public async Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot)
        {
            var meter = _meter;
            var start = meter == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            if (!StateReadMeter.ReadsSuppressed) System.Threading.Interlocked.Increment(ref _storageReads);
            byte[] result;
            if (_overlay.TryGetStorage(address, slot, out var value)) result = value;
            else if (_pendingFlush != null && _pendingFlush.TryGetStorage(address, slot, out var pending, out var pendingCleared))
                result = pendingCleared ? null : pending;
            else if (_cache != null && _cache.TryGetStorage(address, slot, out var cached))
                result = ReferenceEquals(cached, FlatStateCache.Tombstone) ? null : cached;
            else
                result = await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
            meter?.AddStorageRead(System.Diagnostics.Stopwatch.GetTimestamp() - start, address, slot);
            return result;
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => SaveStorageAsync(EvmAddress.FromHex(address), slot, value);

        public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value)
        {
            if (!_buffering) return _inner.SaveStorageAsync(address, slot, value);
            _overlay.SaveStorage(address, slot, value);
            return Task.CompletedTask;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => GetAllStorageAsync(EvmAddress.FromHex(address));

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address)
            => _overlay.MergeAllStorageAsync(_inner, address);

        public Task ClearStorageAsync(string address) => ClearStorageAsync(EvmAddress.FromHex(address));

        public Task ClearStorageAsync(EvmAddress address)
        {
            if (!_buffering) return _inner.ClearStorageAsync(address);
            _overlay.ClearStorage(address);
            return Task.CompletedTask;
        }

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var meter = _meter;
            var start = meter == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            byte[] result;
            if (_overlay.TryGetCode(codeHash, out var c)) result = c;
            else if (_pendingFlush != null && _pendingFlush.TryGetCode(codeHash, out var pending))
                result = pending;
            else if (_cache != null && codeHash != null && codeHash.Length == 32 && _cache.TryGetCode(codeHash, out var cached))
                result = cached;
            else
                result = await _inner.GetCodeAsync(codeHash).ConfigureAwait(false);
            meter?.AddCodeRead(System.Diagnostics.Stopwatch.GetTimestamp() - start, codeHash, result);
            return result;
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            _meter?.AddCodeWrite(code);
            if (!_buffering) return _inner.SaveCodeAsync(codeHash, code);
            _overlay.SaveCode(codeHash, code);
            return Task.CompletedTask;
        }

        public long WindowApproxBytes => _overlay.ApproxBytes();

        public Task<IStateSnapshot> CreateSnapshotAsync() => Task.FromResult(_overlay.CreateSnapshot());

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            _overlay.CommitSnapshot(snapshot);
            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            _overlay.RestoreSnapshot(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
            => _buffering
                ? Task.FromResult<IReadOnlyCollection<string>>(HexOf(_overlay.DirtyAccountAddresses()))
                : _inner.GetDirtyAccountAddressesAsync();

        public Task<IReadOnlyCollection<EvmAddress>> GetDirtyAccountsAsync()
            => _buffering
                ? Task.FromResult(_overlay.DirtyAccountAddresses())
                : _inner.GetDirtyAccountsAsync();

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => GetDirtyStorageSlotsAsync(EvmAddress.FromHex(address));

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(EvmAddress address)
            => _buffering
                ? Task.FromResult(_overlay.DirtyStorageSlots(address))
                : _inner.GetDirtyStorageSlotsAsync(address);

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => _buffering
                ? Task.FromResult<IReadOnlyCollection<string>>(HexOf(_overlay.StorageClearedAddresses()))
                : _inner.GetStorageClearedAddressesAsync();

        public Task<IReadOnlyCollection<EvmAddress>> GetStorageClearedAccountsAsync()
            => _buffering
                ? Task.FromResult(_overlay.StorageClearedAddresses())
                : _inner.GetStorageClearedAccountsAsync();

        public Task ClearDirtyTrackingAsync()
        {
            if (!_buffering) return _inner.ClearDirtyTrackingAsync();
            _overlay.ClearDirtyTracking();
            ClearAddressHashCache();
            return Task.CompletedTask;
        }

        private static List<string> HexOf(IReadOnlyCollection<EvmAddress> addresses)
        {
            var hex = new List<string>(addresses.Count);
            foreach (var address in addresses) hex.Add(address.ToHexLower());
            return hex;
        }


        public void BeginBuffering() => _buffering = true;

        public async Task FlushBufferAsync()
        {
            var (deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code) = MaterializeCategories();

            if (_inner is IFlatStateBatchWriter batchWriter)
            {
                var batch = FlatStateBatch.FromAddresses(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

                if (!batch.IsEmpty)
                    await batchWriter.ApplyBatchAsync(batch).ConfigureAwait(false);
            }
            else
            {
                foreach (var address in deletedAccounts)
                    await _inner.DeleteAccountAsync(address).ConfigureAwait(false);

                foreach (var address in clearedStorage)
                    await _inner.ClearStorageAsync(address).ConfigureAwait(false);

                foreach (var (address, slot, value) in nonZeroStorage)
                    await _inner.SaveStorageAsync(address, slot, value).ConfigureAwait(false);

                foreach (var (address, slot) in deletedSlots)
                    await _inner.SaveStorageAsync(address, slot, Array.Empty<byte>()).ConfigureAwait(false);

                foreach (var (address, account) in accounts)
                    await _inner.SaveAccountAsync(address, account).ConfigureAwait(false);

                foreach (var (codeHash, codeBytes) in code)
                    await _inner.SaveCodeAsync(codeHash, codeBytes).ConfigureAwait(false);
            }

            if (_cache != null)
                PublishToCache(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

            _overlay.ClearWrites();
            _buffering = false;
        }

        public Task<FlatStateBatch> CaptureBufferAsync()
        {
            var (deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code) = MaterializeCategories();
            var batch = FlatStateBatch.FromAddresses(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

            _overlay.ClearWrites();
            _buffering = false;
            return Task.FromResult(batch);
        }

        private (List<EvmAddress> DeletedAccounts, List<EvmAddress> ClearedStorage,
            List<(EvmAddress Address, BigInteger Slot, byte[] Value)> NonZeroStorage,
            List<(EvmAddress Address, BigInteger Slot)> DeletedSlots,
            List<(EvmAddress Address, Account Account)> Accounts,
            List<(byte[] CodeHash, byte[] Code)> Code) MaterializeCategories()
        {
            return (
                ToList(_overlay.DeletedAccountAddresses()),
                ToList(_overlay.ClearedStorageAddresses()),
                ToList(_overlay.BufferedNonZeroStorage()),
                ToList(_overlay.IndividuallyDeletedSlotsNotCleared()),
                ToList(_overlay.BufferedAccounts()),
                ToList(_overlay.BufferedCode()));
        }

        private static List<T> ToList<T>(IEnumerable<T> source)
        {
            var list = new List<T>();
            foreach (var item in source) list.Add(item);
            return list;
        }

        private void PublishToCache(
            List<EvmAddress> deletedAccounts,
            List<EvmAddress> clearedStorage,
            List<(EvmAddress Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            List<(EvmAddress Address, BigInteger Slot)> deletedSlots,
            List<(EvmAddress Address, Account Account)> accounts,
            List<(byte[] CodeHash, byte[] Code)> code)
        {
            foreach (var address in deletedAccounts)
                _cache.PutAccountAbsent(address);

            foreach (var address in clearedStorage)
                _cache.RemoveOwnerStorage(address);

            foreach (var (address, slot, value) in nonZeroStorage)
                _cache.PutStorage(address, slot, value);

            foreach (var (address, slot) in deletedSlots)
                _cache.PutStorageAbsent(address, slot);

            foreach (var (address, account) in accounts)
                _cache.PutAccount(address, account);

            foreach (var (codeHash, codeBytes) in code)
            {
                if (codeHash != null && codeHash.Length == 32)
                    _cache.PutCode(codeHash, codeBytes);
            }
        }

        public Task DiscardBufferAsync()
        {
            _overlay.ClearWrites();
            _buffering = false;
            return Task.CompletedTask;
        }


        public void ClearCache() => _cache?.Clear();
    }
}
