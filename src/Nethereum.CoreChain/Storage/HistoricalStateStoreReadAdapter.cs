using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class HistoricalStateStoreReadAdapter : IStateStore
    {
        private readonly IHistoricalStateProvider _historyProvider;
        private readonly IStateStore _liveStateStore;
        private readonly BigInteger _atBlockNumber;

        public HistoricalStateStoreReadAdapter(
            IHistoricalStateProvider historyProvider,
            IStateStore liveStateStore,
            BigInteger atBlockNumber)
        {
            _historyProvider = historyProvider ?? throw new ArgumentNullException(nameof(historyProvider));
            _liveStateStore = liveStateStore ?? throw new ArgumentNullException(nameof(liveStateStore));
            _atBlockNumber = atBlockNumber;
        }

        public Task<Account> GetAccountAsync(string address)
            => _historyProvider.GetAccountAtBlockAsync(address, _atBlockNumber);

        public async Task<bool> AccountExistsAsync(string address)
        {
            var acc = await GetAccountAsync(address).ConfigureAwait(false);
            return acc != null;
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
            => _historyProvider.GetStorageAtBlockAsync(address, slot, _atBlockNumber);

        public Task<byte[]> GetCodeAsync(byte[] codeHash) => _liveStateStore.GetCodeAsync(codeHash);

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
            => Task.FromResult(new Dictionary<string, Account>());

        public async System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
            => Task.FromResult(new Dictionary<byte[], byte[]>(Nethereum.Util.ByteArrayComparer.Current));

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        public Task ClearDirtyTrackingAsync() => Task.CompletedTask;

        public Task SaveAccountAsync(string address, Account account)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task DeleteAccountAsync(string address)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task ClearStorageAsync(string address)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
            => throw new InvalidOperationException("HistoricalStateStoreReadAdapter is read-only; wrap in ReadOnlyStateStoreWrapper before writing.");

        public Task<IStateSnapshot> CreateSnapshotAsync()
            => Task.FromResult<IStateSnapshot>(new NoOpSnapshot());

        public Task CommitSnapshotAsync(IStateSnapshot snapshot) => Task.CompletedTask;
        public Task RevertSnapshotAsync(IStateSnapshot snapshot) => Task.CompletedTask;

        private sealed class NoOpSnapshot : IStateSnapshot
        {
            public int SnapshotId => 0;
            public void SetAccount(string address, Account account) { }
            public void SetStorage(string address, BigInteger slot, byte[] value) { }
            public void SetCode(byte[] codeHash, byte[] code) { }
            public void DeleteAccount(string address) { }
            public void ClearStorage(string address) { }
            public void Dispose() { }
        }
    }
}
