using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class FollowerStateAddressBytesTests : IDisposable
    {
        private const int AddressCount = 64;
        private const int SlotsPerAddress = 3;
        private const long MaxBytesPerRead = 768;

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "necc-addrbytes-" + Guid.NewGuid().ToString("N"));

        public FollowerStateAddressBytesTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static EvmAddress[] Addresses() => Enumerable.Range(1, AddressCount)
            .Select(i => EvmAddress.From(Enumerable.Repeat((byte)i, 20).ToArray()))
            .ToArray();

        private static Account AccountWith(ulong nonce) => new Account
        {
            Nonce = nonce,
            Balance = 1000 + nonce,
            StateRoot = Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH,
            CodeHash = Nethereum.Model.DefaultValues.EMPTY_DATA_HASH
        };

        private static async Task RunBlockAsync(HistoricalStateStore state, EvmAddress[] addresses, ulong nonce)
        {
            state.SetCurrentBlockNumber(nonce);
            foreach (var address in addresses)
            {
                var account = await state.GetAccountAsync(address);
                await state.AccountExistsAsync(address);
                for (var slot = 0; slot < SlotsPerAddress; slot++)
                    await state.GetStorageAsync(address, slot);
                await state.SaveAccountAsync(address, AccountWith(nonce));
                for (var slot = 0; slot < SlotsPerAddress; slot++)
                    await state.SaveStorageAsync(address, slot, new byte[] { (byte)(nonce + 1), (byte)slot });
            }
            await state.GetDirtyAccountsAsync();
            await state.DrainBufferToDiskAsync();
            await state.RecordBlockDiffAsync();
            await state.ClearDirtyTrackingAsync();
        }

        [Fact]
        public async Task Given_TheFollowerWrappersWithStringRefusingSeamsBetweenThem_When_ABlockOfAccessesAndAFlushRun_Then_NoStringAddressCrossesAnySeam()
        {
            var terminal = new EvmAddressOnlyStateStore();
            var tap = new StringRefusingTap(new BufferedFlatStateStore(terminal, new FlatStateCache(1 << 20), new PendingFlushFlatOverlay()));
            var state = new HistoricalStateStore(tap, new InMemoryStateDiffStore(), HistoricalStateOptions.Default);
            var addresses = Addresses();

            await RunBlockAsync(state, addresses, nonce: 1);
            await RunBlockAsync(state, addresses, nonce: 2);

            Assert.Equal(0, tap.StringCalls);
            Assert.Equal(0, terminal.StringCalls);
            Assert.True(terminal.EvmAddressCalls > 0);
            Assert.Equal(AddressCount, terminal.AccountCount);
        }

        [Fact]
        public async Task Given_ADirtyKeyObserverOnTheFollowerWrappers_When_AccountsAndSlotsAreWritten_Then_ItIsToldTheEvmAddressOfEachDirtyKey()
        {
            var tap = new StringRefusingTap(new BufferedFlatStateStore(new EvmAddressOnlyStateStore()));
            var state = new HistoricalStateStore(tap, new InMemoryStateDiffStore(), HistoricalStateOptions.Default);
            var observer = new RecordingObserver();
            Assert.True(state.TryAttachDirtyKeyObserver(observer));
            var addresses = Addresses();

            await RunBlockAsync(state, addresses, nonce: 1);

            Assert.Equal(addresses.OrderBy(a => a.ToHexLower()), observer.Accounts.OrderBy(a => a.ToHexLower()));
            Assert.Equal(AddressCount * SlotsPerAddress, observer.Slots.Count);
            Assert.All(observer.Slots, s => Assert.Contains(s.Address, addresses));
        }

        [Fact]
        public async Task Given_ARocksDbBackedFollowerStack_When_ReadsGoThroughTheEvmAddressApi_Then_EachReadAllocatesLessThanTheMeasuredBound()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var raw = new RocksDbStateStore(manager);
            var state = (HistoricalStateStore)new StateLayer().Stores.MainnetFollower(
                raw, new InMemoryStateDiffStore(), HistoricalStateOptions.Default, new FlatStateCache(1 << 20), new PendingFlushFlatOverlay());
            var addresses = Addresses();
            await RunBlockAsync(state, addresses, nonce: 1);

            ReadAll(state, addresses);
            var native = AllocatedBy(() => ReadAll(state, addresses));

            var readsPerPass = AddressCount * (1 + SlotsPerAddress);
            Assert.True(native <= MaxBytesPerRead * readsPerPass,
                $"the EvmAddress API allocated {native} bytes over {readsPerPass} reads");
        }

        private static void ReadAll(IStateStore state, EvmAddress[] addresses)
        {
            foreach (var address in addresses)
            {
                state.GetAccountAsync(address).GetAwaiter().GetResult();
                for (var slot = 0; slot < SlotsPerAddress; slot++)
                    state.GetStorageAsync(address, slot).GetAwaiter().GetResult();
            }
        }

        private static long AllocatedBy(Action work)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before = GC.GetAllocatedBytesForCurrentThread();
            work();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private sealed class StringRefusingTap : IStateStore, IStateWriteBuffer, IDirtyKeySource, IAddressHashCache
        {
            private readonly BufferedFlatStateStore _inner;

            public StringRefusingTap(BufferedFlatStateStore inner) { _inner = inner; }

            public int StringCalls { get; private set; }

            private Exception Refuse()
            {
                StringCalls++;
                return new InvalidOperationException("a string address crossed between the follower wrappers");
            }

            public Task<Account> GetAccountAsync(string address) => throw Refuse();
            public Task SaveAccountAsync(string address, Account account) => throw Refuse();
            public Task<bool> AccountExistsAsync(string address) => throw Refuse();
            public Task DeleteAccountAsync(string address) => throw Refuse();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => throw Refuse();
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => throw Refuse();
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => throw Refuse();
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => throw Refuse();
            public Task ClearStorageAsync(string address) => throw Refuse();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => throw Refuse();
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => throw Refuse();
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => throw Refuse();
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => throw Refuse();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => throw Refuse();

            public Task<Account> GetAccountAsync(EvmAddress address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(EvmAddress address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(EvmAddress address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(EvmAddress address) => _inner.DeleteAccountAsync(address);
            public Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(EvmAddress address) => _inner.ClearStorageAsync(address);
            public Task<IReadOnlyCollection<EvmAddress>> GetDirtyAccountsAsync() => _inner.GetDirtyAccountsAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(EvmAddress address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<EvmAddress>> GetStorageClearedAccountsAsync() => _inner.GetStorageClearedAccountsAsync();
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

            public void BeginBuffering() => _inner.BeginBuffering();
            public Task FlushBufferAsync() => _inner.FlushBufferAsync();
            public Task DiscardBufferAsync() => _inner.DiscardBufferAsync();
            public Task<FlatStateBatch> CaptureBufferAsync() => _inner.CaptureBufferAsync();

            public bool TryAttachDirtyKeyObserver(IDirtyKeyObserver observer) => _inner.TryAttachDirtyKeyObserver(observer);
            public void DetachDirtyKeyObserver(IDirtyKeyObserver observer) => _inner.DetachDirtyKeyObserver(observer);

            public bool TryGetAddressHash(EvmAddress address, out byte[] hash) => _inner.TryGetAddressHash(address, out hash);
            public void SetAddressHash(EvmAddress address, byte[] hash) => _inner.SetAddressHash(address, hash);
            public void ClearAddressHashCache() => _inner.ClearAddressHashCache();
        }

        private sealed class RecordingObserver : IDirtyKeyObserver
        {
            public List<EvmAddress> Accounts { get; } = new List<EvmAddress>();
            public List<(EvmAddress Address, BigInteger Slot)> Slots { get; } = new List<(EvmAddress, BigInteger)>();

            public void OnAccountDirty(EvmAddress address) => Accounts.Add(address);

            public void OnStorageSlotDirty(EvmAddress address, BigInteger slot) => Slots.Add((address, slot));
        }

        private sealed class EvmAddressOnlyStateStore : IStateStore
        {
            private readonly Dictionary<EvmAddress, Account> _accounts = new Dictionary<EvmAddress, Account>();
            private readonly Dictionary<(EvmAddress, BigInteger), byte[]> _storage = new Dictionary<(EvmAddress, BigInteger), byte[]>();

            public int StringCalls { get; private set; }
            public int EvmAddressCalls { get; private set; }
            public int AccountCount => _accounts.Count;

            private static Account Copy(Account account) => new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                StateRoot = account.StateRoot,
                CodeHash = account.CodeHash
            };

            private Exception Refuse()
            {
                StringCalls++;
                return new InvalidOperationException("a string address reached the terminal store");
            }

            public Task<Account> GetAccountAsync(string address) => throw Refuse();
            public Task SaveAccountAsync(string address, Account account) => throw Refuse();
            public Task<bool> AccountExistsAsync(string address) => throw Refuse();
            public Task DeleteAccountAsync(string address) => throw Refuse();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => throw Refuse();
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => throw Refuse();
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => throw Refuse();
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => throw Refuse();
            public Task ClearStorageAsync(string address) => throw Refuse();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => throw Refuse();
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => throw Refuse();
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => throw Refuse();
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => throw Refuse();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => throw Refuse();

            public Task<Account> GetAccountAsync(EvmAddress address)
            {
                EvmAddressCalls++;
                return Task.FromResult(_accounts.TryGetValue(address, out var account) ? Copy(account) : null);
            }

            public Task SaveAccountAsync(EvmAddress address, Account account)
            {
                EvmAddressCalls++;
                _accounts[address] = Copy(account);
                return Task.CompletedTask;
            }

            public Task<bool> AccountExistsAsync(EvmAddress address)
            {
                EvmAddressCalls++;
                return Task.FromResult(_accounts.ContainsKey(address));
            }

            public Task DeleteAccountAsync(EvmAddress address)
            {
                EvmAddressCalls++;
                _accounts.Remove(address);
                return Task.CompletedTask;
            }

            public Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot)
            {
                EvmAddressCalls++;
                return Task.FromResult(_storage.TryGetValue((address, slot), out var value) ? value : null);
            }

            public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value)
            {
                EvmAddressCalls++;
                _storage[(address, slot)] = value;
                return Task.CompletedTask;
            }

            public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value)
            {
                EvmAddressCalls++;
                return Task.CompletedTask;
            }

            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address)
            {
                EvmAddressCalls++;
                return Task.FromResult(new Dictionary<byte[], byte[]>(ByteArrayComparer.Current));
            }

            public Task ClearStorageAsync(EvmAddress address)
            {
                EvmAddressCalls++;
                foreach (var key in _storage.Keys.Where(k => k.Item1 == address).ToList()) _storage.Remove(key);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyCollection<EvmAddress>> GetDirtyAccountsAsync()
                => Task.FromResult<IReadOnlyCollection<EvmAddress>>(Array.Empty<EvmAddress>());

            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(EvmAddress address)
                => Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());

            public Task<IReadOnlyCollection<EvmAddress>> GetStorageClearedAccountsAsync()
                => Task.FromResult<IReadOnlyCollection<EvmAddress>>(Array.Empty<EvmAddress>());

            public Task<byte[]> GetCodeAsync(byte[] codeHash) => Task.FromResult<byte[]>(null);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => Task.CompletedTask;
            public Task<IStateSnapshot> CreateSnapshotAsync() => throw new NotSupportedException();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => throw new NotSupportedException();
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => throw new NotSupportedException();
            public Task ClearDirtyTrackingAsync() => Task.CompletedTask;
        }
    }
}
