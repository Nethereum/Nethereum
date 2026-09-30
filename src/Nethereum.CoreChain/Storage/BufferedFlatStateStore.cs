using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public sealed class BufferedFlatStateStore : IStateStore, IStateWriteBuffer, ISnapFlatStateWriter, IFlatCacheInvalidatable, IStateReadStats
    {
        private readonly IStateStore _inner;
        private readonly StateOverlay _overlay = new();
        private readonly FlatStateCache _cache;
        private readonly IPendingFlushFlatOverlay _pendingFlush;
        private bool _buffering;

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


        public async Task<Account> GetAccountAsync(string address)
        {
            System.Threading.Interlocked.Increment(ref _accountReads);
            var key = StateOverlay.Normalize(address);
            if (_overlay.TryGetAccount(key, out var account)) return account;
            if (_pendingFlush != null && _pendingFlush.TryGetAccount(address, out var pending, out var pendingDeleted))
                return pendingDeleted ? null : pending;
            if (_cache != null && _cache.TryGetAccount(address, out var cached))
                return ReferenceEquals(cached, FlatStateCache.AccountTombstone) ? null : cached;
            return await _inner.GetAccountAsync(address).ConfigureAwait(false);
        }

        public Task SaveAccountAsync(string address, Account account)
        {
            if (!_buffering) return _inner.SaveAccountAsync(address, account);
            _overlay.SaveAccount(StateOverlay.Normalize(address), StateOverlay.OriginalAddress(address), account);
            return Task.CompletedTask;
        }

        public async Task<bool> AccountExistsAsync(string address)
        {
            var resolved = _overlay.AccountExists(StateOverlay.Normalize(address));
            if (resolved.HasValue) return resolved.Value;
            return await _inner.AccountExistsAsync(address).ConfigureAwait(false);
        }

        public Task DeleteAccountAsync(string address)
        {
            if (!_buffering) return _inner.DeleteAccountAsync(address);
            _overlay.DeleteAccount(StateOverlay.Normalize(address), StateOverlay.OriginalAddress(address));
            return Task.CompletedTask;
        }

        public async Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            var merged = await _inner.GetAllAccountsAsync().ConfigureAwait(false);
            foreach (var kv in _overlay.LiveAccountsByOriginalAddress())
                merged[kv.Key] = kv.Value;
            foreach (var deletedOriginal in _overlay.DeletedOriginalAddresses())
                merged.Remove(deletedOriginal);
            return merged;
        }

        public async IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _overlay.LiveAccountsByOriginalAddress())
            {
                seen.Add(kv.Key);
                yield return kv;
            }
            await foreach (var kv in _inner.StreamAccountsAsync().ConfigureAwait(false))
            {
                if (seen.Contains(kv.Key)) continue;
                if (_overlay.IsAccountDeletedByKey(StateOverlay.Normalize(kv.Key))) continue;
                yield return kv;
            }
        }

        public async Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            System.Threading.Interlocked.Increment(ref _storageReads);
            var key = StateOverlay.Normalize(address);
            if (_overlay.TryGetStorage(key, slot, out var value)) return value;
            if (_pendingFlush != null && _pendingFlush.TryGetStorage(address, slot, out var pending, out var pendingCleared))
                return pendingCleared ? null : pending;
            if (_cache != null && _cache.TryGetStorage(address, slot, out var cached))
                return ReferenceEquals(cached, FlatStateCache.Tombstone) ? null : cached;
            return await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            if (!_buffering) return _inner.SaveStorageAsync(address, slot, value);
            _overlay.SaveStorage(StateOverlay.Normalize(address), StateOverlay.OriginalAddress(address), slot, value);
            return Task.CompletedTask;
        }

        public async Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
        {
            var key = StateOverlay.Normalize(address);
            Dictionary<byte[], byte[]> result;
            if (_overlay.IsStorageCleared(key))
            {
                result = new Dictionary<byte[], byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            }
            else
            {
                result = await _inner.GetAllStorageAsync(address).ConfigureAwait(false);
                foreach (var slot in _overlay.DeletedStorageSlots(key))
                    result.Remove(StateKeys.StorageSlotKey(slot));
            }
            foreach (var kv in _overlay.StorageEntries(key))
                result[StateKeys.StorageSlotKey(kv.Key)] = kv.Value;
            return result;
        }

        public Task ClearStorageAsync(string address)
        {
            if (!_buffering) return _inner.ClearStorageAsync(address);
            _overlay.ClearStorage(StateOverlay.Normalize(address), StateOverlay.OriginalAddress(address));
            return Task.CompletedTask;
        }

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            if (_overlay.TryGetCode(hex, out var c)) return c;
            if (_pendingFlush != null && _pendingFlush.TryGetCode(codeHash, out var pending))
                return pending;
            if (_cache != null && codeHash != null && codeHash.Length == 32 && _cache.TryGetCode(codeHash, out var cached))
                return cached;
            return await _inner.GetCodeAsync(codeHash).ConfigureAwait(false);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            if (!_buffering) return _inner.SaveCodeAsync(codeHash, code);
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            _overlay.SaveCode(hex, code);
            return Task.CompletedTask;
        }

        public long WindowApproxBytes => _overlay.ApproxBytes();

        public Task<IStateSnapshot> CreateSnapshotAsync() => Task.FromResult(_overlay.CreateSnapshot());

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            _overlay.RestoreSnapshot(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
            => _buffering
                ? Task.FromResult(_overlay.DirtyAccountAddresses())
                : _inner.GetDirtyAccountAddressesAsync();

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => _buffering
                ? Task.FromResult(_overlay.DirtyStorageSlots(StateOverlay.OriginalAddress(address)))
                : _inner.GetDirtyStorageSlotsAsync(address);

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => _buffering
                ? Task.FromResult(_overlay.StorageClearedAddresses())
                : _inner.GetStorageClearedAddressesAsync();

        public Task ClearDirtyTrackingAsync()
        {
            if (!_buffering) return _inner.ClearDirtyTrackingAsync();
            _overlay.ClearDirtyTracking();
            return Task.CompletedTask;
        }


        public void BeginBuffering() => _buffering = true;

        public async Task FlushBufferAsync()
        {
            var (deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code) = MaterializeCategories();

            if (_inner is IFlatStateBatchWriter batchWriter)
            {
                var batch = new FlatStateBatch(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

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
            var batch = new FlatStateBatch(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

            _overlay.ClearWrites();
            _buffering = false;
            return Task.FromResult(batch);
        }

        private (List<string> DeletedAccounts, List<string> ClearedStorage,
            List<(string Address, BigInteger Slot, byte[] Value)> NonZeroStorage,
            List<(string Address, BigInteger Slot)> DeletedSlots,
            List<(string Address, Account Account)> Accounts,
            List<(byte[] CodeHash, byte[] Code)> Code) MaterializeCategories()
        {
            return (
                ToList(_overlay.DeletedAccountOriginalAddresses()),
                ToList(_overlay.ClearedStorageOriginalAddresses()),
                ToList(_overlay.BufferedNonZeroStorage()),
                ToList(_overlay.IndividuallyDeletedSlotsNotCleared()),
                CloneAccounts(_overlay.BufferedAccounts()),
                ToList(_overlay.BufferedCode()));
        }

        private static List<T> ToList<T>(IEnumerable<T> source)
        {
            var list = new List<T>();
            foreach (var item in source) list.Add(item);
            return list;
        }

        private static List<(string Address, Account Account)> CloneAccounts(
            IEnumerable<(string OriginalAddress, Account Account)> source)
        {
            var list = new List<(string, Account)>();
            foreach (var item in source) list.Add((item.OriginalAddress, item.Account.Clone()));
            return list;
        }

        private void PublishToCache(
            List<string> deletedAccounts,
            List<string> clearedStorage,
            List<(string Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            List<(string Address, BigInteger Slot)> deletedSlots,
            List<(string Address, Account Account)> accounts,
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
