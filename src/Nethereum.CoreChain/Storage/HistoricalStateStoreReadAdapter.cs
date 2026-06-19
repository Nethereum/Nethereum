using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// <see cref="IStateStore"/> facade that serves reads from an
    /// <see cref="IHistoricalStateProvider"/> at a fixed historical block
    /// number. Used by the read-only witness-capture path:
    /// wrap this in a <see cref="ReadOnlyStateStoreWrapper"/> and hand it
    /// to <see cref="BlockExecutor.ExecuteAsync"/> with
    /// <see cref="BlockExecutionOptions.ReadOnly"/> set, so the engine
    /// reads parent-block state and absorbs any writes in memory.
    ///
    /// <para>Mutation surface: <see cref="SaveAccountAsync"/> etc throw —
    /// callers must place a <see cref="ReadOnlyStateStoreWrapper"/> in
    /// front before any write path is exercised. <see cref="GetCodeAsync"/>
    /// falls through to the live <paramref name="liveStateStore"/> code-by-hash
    /// table (code blobs are content-addressed and immutable, so the live
    /// store is correct historically). Dirty-tracking and snapshot APIs
    /// return empty / no-op shapes; the wrapping
    /// <see cref="ReadOnlyStateStoreWrapper"/> overrides them with its
    /// overlay-backed implementations.</para>
    /// </summary>
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

        // Code is content-addressed: the same code hash always maps to the
        // same bytes. Reading from the live store is historically correct.
        public Task<byte[]> GetCodeAsync(byte[] codeHash) => _liveStateStore.GetCodeAsync(codeHash);

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
            => Task.FromResult(new Dictionary<string, Account>());

        public async System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
        {
            // Historical full-state stream is not supported; witness capture
            // doesn't need it (reads are demand-driven through the recorder).
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

        // Mutation surface: a bare HistoricalStateStoreReadAdapter has no
        // write path — callers must place a ReadOnlyStateStoreWrapper in
        // front of it.
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
