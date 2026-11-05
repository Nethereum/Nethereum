using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

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

        public Task<Account> GetAccountAsync(string address) => GetAccountAsync(EvmAddress.FromHex(address));

        public async Task<Account> GetAccountAsync(EvmAddress address)
        {
            if (_overlay.TryGetAccount(address, out var account)) return account;
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

        public Task SaveAccountAsync(string address, Account account) => SaveAccountAsync(EvmAddress.FromHex(address), account);

        public Task SaveAccountAsync(EvmAddress address, Account account)
        {
            _overlay.SaveAccount(address, account);
            return Task.CompletedTask;
        }

        public Task<bool> AccountExistsAsync(string address) => AccountExistsAsync(EvmAddress.FromHex(address));

        public Task<bool> AccountExistsAsync(EvmAddress address)
            => _overlay.AccountExistsAsync(_inner, address);

        public Task DeleteAccountAsync(string address) => DeleteAccountAsync(EvmAddress.FromHex(address));

        public Task DeleteAccountAsync(EvmAddress address)
        {
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
            if (_overlay.TryGetStorage(address, slot, out var value)) return value;
            return await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => throw new System.NotSupportedException(
                "ReadOnlyStateStoreWrapper does not support keccak-keyed writes.");

        public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value)
            => throw new System.NotSupportedException(
                "ReadOnlyStateStoreWrapper does not support keccak-keyed writes.");

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => SaveStorageAsync(EvmAddress.FromHex(address), slot, value);

        public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value)
        {
            _overlay.SaveStorage(address, slot, value);
            return Task.CompletedTask;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => GetAllStorageAsync(EvmAddress.FromHex(address));

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address)
            => _overlay.MergeAllStorageAsync(_inner, address);

        public Task ClearStorageAsync(string address) => ClearStorageAsync(EvmAddress.FromHex(address));

        public Task ClearStorageAsync(EvmAddress address)
        {
            _overlay.ClearStorage(address);
            return Task.CompletedTask;
        }

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            if (_overlay.TryGetCode(codeHash, out var c)) return c;
            return await _inner.GetCodeAsync(codeHash).ConfigureAwait(false);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            _overlay.SaveCode(codeHash, code);
            return Task.CompletedTask;
        }

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
            => Task.FromResult<IReadOnlyCollection<string>>(HexOf(_overlay.DirtyAccountAddresses()));

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => Task.FromResult(_overlay.DirtyStorageSlots(EvmAddress.FromHex(address)));

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(HexOf(_overlay.StorageClearedAddresses()));

        public Task ClearDirtyTrackingAsync()
        {
            _overlay.ClearDirtyTracking();
            return Task.CompletedTask;
        }

        private static List<string> HexOf(IReadOnlyCollection<EvmAddress> addresses)
        {
            var hex = new List<string>(addresses.Count);
            foreach (var address in addresses) hex.Add(address.ToHexLower());
            return hex;
        }
    }
}
