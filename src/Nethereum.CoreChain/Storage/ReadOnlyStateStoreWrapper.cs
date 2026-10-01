using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public sealed class ReadOnlyStateStoreWrapper : IStateStore
    {
        private readonly IStateStore _inner;
        public IStateStore Inner => _inner;
        private readonly StateOverlay _overlay = new();

        public ReadOnlyStateStoreWrapper(IStateStore inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            var key = StateOverlay.Normalize(address);
            if (_overlay.TryGetAccount(key, out var account)) return account;
            return CopyOfBaseAccount(await _inner.GetAccountAsync(address).ConfigureAwait(false));
        }

        private static Account CopyOfBaseAccount(Account inner)
        {
            if (inner == null) return null;
            return new Account
            {
                Nonce = inner.Nonce,
                Balance = inner.Balance,
                StateRoot = inner.StateRoot,
                CodeHash = inner.CodeHash
            };
        }

        public Task SaveAccountAsync(string address, Account account)
        {
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
            var key = StateOverlay.Normalize(address);
            if (_overlay.TryGetStorage(key, slot, out var value)) return value;
            return await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => throw new System.NotSupportedException(
                "ReadOnlyStateStoreWrapper does not support keccak-keyed writes.");

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
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
            _overlay.ClearStorage(StateOverlay.Normalize(address), StateOverlay.OriginalAddress(address));
            return Task.CompletedTask;
        }

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            if (_overlay.TryGetCode(hex, out var c)) return c;
            return await _inner.GetCodeAsync(codeHash).ConfigureAwait(false);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            var hex = codeHash == null ? "" : Convert.ToHexString(codeHash);
            _overlay.SaveCode(hex, code);
            return Task.CompletedTask;
        }

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
            => Task.FromResult(_overlay.DirtyAccountAddresses());

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => Task.FromResult(_overlay.DirtyStorageSlots(StateOverlay.OriginalAddress(address)));

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => Task.FromResult(_overlay.StorageClearedAddresses());

        public Task ClearDirtyTrackingAsync()
        {
            _overlay.ClearDirtyTracking();
            return Task.CompletedTask;
        }
    }
}
