using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// IStateStore decorator that records every address + slot the engine
    /// writes to during execution. Needed because the engine's state-root
    /// calculator calls ClearDirtyTrackingAsync after compute, so the inner
    /// store's dirty set is empty by the time the BlockReplay validation
    /// block runs. We retain the touched set in our own persistent
    /// dictionaries instead.
    /// </summary>
    public sealed class TouchRecordingStore : IStateStore
    {
        private readonly IStateStore _inner;

        public ConcurrentDictionary<string, byte> TouchedAccounts { get; }
            = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, ConcurrentDictionary<BigInteger, byte>> TouchedSlots { get; }
            = new(StringComparer.OrdinalIgnoreCase);

        public TouchRecordingStore(IStateStore inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private static string Normalize(string address)
            => AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();

        private void RecordAccount(string addr) => TouchedAccounts.TryAdd(Normalize(addr), 0);
        private void RecordSlot(string addr, BigInteger slot)
        {
            var key = Normalize(addr);
            TouchedAccounts.TryAdd(key, 0);
            var slots = TouchedSlots.GetOrAdd(key, _ => new ConcurrentDictionary<BigInteger, byte>());
            slots.TryAdd(slot, 0);
        }

        public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
        public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
        public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
        public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
        public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
        public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);

        public Task SaveAccountAsync(string address, Account account)
        {
            RecordAccount(address);
            return _inner.SaveAccountAsync(address, account);
        }
        public Task DeleteAccountAsync(string address)
        {
            RecordAccount(address);
            return _inner.DeleteAccountAsync(address);
        }
        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            RecordSlot(address, slot);
            return _inner.SaveStorageAsync(address, slot, value);
        }
        public Task ClearStorageAsync(string address)
        {
            RecordAccount(address);
            return _inner.ClearStorageAsync(address);
        }
        public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);

        public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
        public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
        public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
        public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
    }
}
