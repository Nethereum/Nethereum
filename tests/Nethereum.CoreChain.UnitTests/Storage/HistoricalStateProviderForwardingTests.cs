using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class HistoricalStateProviderForwardingTests
    {
        private const string Addr = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task ForwardingDecorator_ArmsJournal_WhenWrappingHistoricalStateStore()
        {
            var inner = new InMemoryStateStore();
            var diffStore = new InMemoryStateDiffStore();
            var historical = new HistoricalStateStore(inner, diffStore, HistoricalStateOptions.FullArchive);
            await historical.SaveAccountAsync(Addr, new Account { Balance = 100, Nonce = 0 });

            var wrapped = new ForwardingDecorator(historical);

            ((IHistoricalStateProvider)wrapped).SetCurrentBlockNumber(1);
            await wrapped.SaveAccountAsync(Addr, new Account { Balance = 200, Nonce = 1 });
            await ((IHistoricalStateProvider)wrapped).ClearCurrentBlockNumberAsync();

            var newest = await diffStore.GetNewestDiffBlockAsync();
            Assert.True(newest.HasValue);
            Assert.Equal((BigInteger)1, newest.Value);
        }

        [Fact]
        public async Task NonForwardingDecorator_BreaksJournal()
        {
            var inner = new InMemoryStateStore();
            var diffStore = new InMemoryStateDiffStore();
            var historical = new HistoricalStateStore(inner, diffStore, HistoricalStateOptions.FullArchive);
            await historical.SaveAccountAsync(Addr, new Account { Balance = 100, Nonce = 0 });

            var brokenWrap = new NonForwardingDecorator(historical);

            var journal = (object)brokenWrap as IHistoricalStateProvider;
            Assert.Null(journal);

            await brokenWrap.SaveAccountAsync(Addr, new Account { Balance = 200, Nonce = 1 });

            var newest = await diffStore.GetNewestDiffBlockAsync();
            Assert.False(newest.HasValue);
        }

        private sealed class ForwardingDecorator : IStateStore, IHistoricalStateProvider
        {
            private readonly IStateStore _inner;
            public ForwardingDecorator(IStateStore inner) { _inner = inner; }

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

            public void SetCurrentBlockNumber(BigInteger blockNumber)
            {
                if (_inner is IHistoricalStateProvider p) p.SetCurrentBlockNumber(blockNumber);
            }
            public Task ClearCurrentBlockNumberAsync()
                => _inner is IHistoricalStateProvider p ? p.ClearCurrentBlockNumberAsync() : Task.CompletedTask;
            public Task<Account> GetAccountAtBlockAsync(string address, BigInteger blockNumber)
                => _inner is IHistoricalStateProvider p ? p.GetAccountAtBlockAsync(address, blockNumber) : Task.FromResult<Account>(null);
            public Task<byte[]> GetStorageAtBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
                => _inner is IHistoricalStateProvider p ? p.GetStorageAtBlockAsync(address, slot, blockNumber) : Task.FromResult<byte[]>(null);
        }

        private sealed class NonForwardingDecorator : IStateStore
        {
            private readonly IStateStore _inner;
            public NonForwardingDecorator(IStateStore inner) { _inner = inner; }

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
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
}
