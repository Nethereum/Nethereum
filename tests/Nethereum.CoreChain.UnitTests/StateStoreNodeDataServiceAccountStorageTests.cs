using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM.BlockchainState;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class StateStoreNodeDataServiceAccountStorageTests
    {
        private const string Address = "0x1234567890123456789012345678901234567890";

        private static byte[] SlotKey(byte last)
        {
            var key = new byte[32];
            key[31] = last;
            return key;
        }

        private static Dictionary<byte[], byte[]> Rows(params byte[][] values)
        {
            var rows = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
            for (var i = 0; i < values.Length; i++) rows[SlotKey((byte)(i + 1))] = values[i];
            return rows;
        }

        private sealed class CannedStorageStore : IStateStore
        {
            private readonly Dictionary<byte[], byte[]> _rows;
            public CannedStorageStore(Dictionary<byte[], byte[]> rows) => _rows = rows;

            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => Task.FromResult(_rows);

            public Task<Account> GetAccountAsync(string address) => throw new NotSupportedException();
            public Task SaveAccountAsync(string address, Account account) => throw new NotSupportedException();
            public Task<bool> AccountExistsAsync(string address) => throw new NotSupportedException();
            public Task DeleteAccountAsync(string address) => throw new NotSupportedException();
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => throw new NotSupportedException();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => throw new NotSupportedException();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => throw new NotSupportedException();
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => throw new NotSupportedException();
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => throw new NotSupportedException();
            public Task ClearStorageAsync(string address) => throw new NotSupportedException();
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => throw new NotSupportedException();
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => throw new NotSupportedException();
            public Task<Nethereum.CoreChain.Storage.IStateSnapshot> CreateSnapshotAsync() => throw new NotSupportedException();
            public Task CommitSnapshotAsync(Nethereum.CoreChain.Storage.IStateSnapshot snapshot) => throw new NotSupportedException();
            public Task RevertSnapshotAsync(Nethereum.CoreChain.Storage.IStateSnapshot snapshot) => throw new NotSupportedException();
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => throw new NotSupportedException();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => throw new NotSupportedException();
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => throw new NotSupportedException();
            public Task ClearDirtyTrackingAsync() => throw new NotSupportedException();
        }

        private static Task<bool> AskAsync(IStateStore store) =>
            new StateStoreNodeDataService(store).AccountHasStorageAsync(Address);

        [Fact]
        public async Task Given_ARealStoreHoldingAValue_When_Asked_Then_TheAccountHasStorage()
        {
            var store = new InMemoryStateStore();
            await store.SaveStorageAsync(Address, BigInteger.One, new EvmUInt256(0x42).ToBigEndian());

            Assert.True(await AskAsync(store));
        }

        [Fact]
        public async Task Given_ARealStoreWithNothingUnderTheAddress_When_Asked_Then_TheAccountHasNoStorage()
        {
            Assert.False(await AskAsync(new InMemoryStateStore()));
        }

        [Fact]
        public async Task Given_ARealStoreWhoseOnlyValueWasZeroedOut_When_Asked_Then_TheAccountHasNoStorage()
        {
            var store = new InMemoryStateStore();
            await store.SaveStorageAsync(Address, BigInteger.One, new EvmUInt256(0x42).ToBigEndian());
            await store.SaveStorageAsync(Address, BigInteger.One, new byte[32]);

            Assert.False(await AskAsync(store));
        }

        [Fact]
        public async Task Given_ARowWhoseValueIsAllZeroBytes_When_Asked_Then_TheAccountHasNoStorage()
        {
            Assert.False(await AskAsync(new CannedStorageStore(Rows(new byte[32]))));
        }

        [Fact]
        public async Task Given_ARowWhoseValueIsNull_When_Asked_Then_TheAccountHasNoStorage()
        {
            Assert.False(await AskAsync(new CannedStorageStore(Rows(new byte[][] { null }))));
        }

        [Fact]
        public async Task Given_ZeroAndNullRowsBesideANonZeroOne_When_Asked_Then_TheAccountHasStorage()
        {
            var rows = Rows(new byte[32], null, new byte[] { 0x01 });

            Assert.True(await AskAsync(new CannedStorageStore(rows)));
        }

        [Fact]
        public async Task Given_ARowHoldingASingleTrimmedNonZeroByte_When_Asked_Then_TheAccountHasStorage()
        {
            Assert.True(await AskAsync(new CannedStorageStore(Rows(new byte[] { 0x01 }))));
        }

        [Fact]
        public async Task Given_TheStoreReturnsNoRowsAtAll_When_Asked_Then_TheAccountHasNoStorage()
        {
            Assert.False(await AskAsync(new CannedStorageStore(null)));
        }

        [Fact]
        public async Task Given_TheExecutionOverlayHasReadNothing_When_Asked_Then_TheStoreStillSettlesIt()
        {
            var store = new InMemoryStateStore();
            await store.SaveStorageAsync(Address, BigInteger.One, new EvmUInt256(0x42).ToBigEndian());
            var executionState = new ExecutionStateService(new StateStoreNodeDataService(store));

            Assert.Empty(executionState.AccountsState);
            Assert.True(await executionState.AccountHasStorageAsync(Address));
        }
    }
}
