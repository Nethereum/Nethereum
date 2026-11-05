using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class SnapFlatStateWriterContractTests
    {
        private static byte[] Hash32(byte fill)
        {
            var h = new byte[32];
            for (var i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        [Fact]
        public void Given_AStorageValueOfAllZeroBytes_When_TheContractIsAsked_Then_ItReportsTheSlotAsCleared()
        {
            Assert.True(SnapFlatStorageValue.ClearsSlot(null));
            Assert.True(SnapFlatStorageValue.ClearsSlot(Array.Empty<byte>()));
            Assert.True(SnapFlatStorageValue.ClearsSlot(new byte[32]));
            Assert.True(SnapFlatStorageValue.ClearsSlot(new byte[] { 0x00, 0x00 }));
        }

        [Fact]
        public void Given_AStorageValueWithAnyNonZeroByte_When_TheContractIsAsked_Then_ItReportsTheSlotAsHoldingAValue()
        {
            Assert.False(SnapFlatStorageValue.ClearsSlot(new byte[] { 0x01 }));
            Assert.False(SnapFlatStorageValue.ClearsSlot(new byte[] { 0x00, 0x01 }));
            Assert.False(SnapFlatStorageValue.ClearsSlot(new byte[] { 0xFF }));
        }

        [Fact]
        public async Task Given_AnInMemoryStateStore_When_ASnapFlatSlotIsWritten_Then_ItRefusesLoudlyRatherThanDroppingTheWrite()
        {
            var store = new InMemoryStateStore();

            await Assert.ThrowsAsync<NotSupportedException>(
                () => store.SaveStorageByHashAsync(Hash32(0x01), Hash32(0x02), new byte[] { 0x0A }));
        }

        [Fact]
        public async Task Given_AnInMemoryStateStore_When_AFlatAccountIsWrittenAndReadBack_Then_ItRoundTripsByHash()
        {
            var store = new InMemoryStateStore();
            var accountHash = Hash32(0x03);
            await store.SaveAccountByHashAsync(accountHash, new Account { Balance = new EvmUInt256(7) });

            var read = await store.GetAccountByHashAsync(accountHash);

            Assert.Equal(new EvmUInt256(7), read.Balance);
        }

        [Fact]
        public async Task Given_AnInMemoryStateStore_When_AFlatAccountIsDeletedByHash_Then_ItIsGone()
        {
            var store = new InMemoryStateStore();
            var accountHash = Hash32(0x04);
            await store.SaveAccountByHashAsync(accountHash, new Account { Balance = new EvmUInt256(7) });

            await store.DeleteAccountByHashAsync(accountHash);

            Assert.Null(await store.GetAccountByHashAsync(accountHash));
        }

        [Fact]
        public async Task Given_AWrapperOverAStoreThatCannotWriteFlatRows_When_AFlatRowIsWritten_Then_ItRefusesLoudlyRatherThanNoOpping()
        {
            var buffered = new BufferedFlatStateStore(new NonFlatStateStore());
            var historical = new HistoricalStateStore(new NonFlatStateStore());

            await Assert.ThrowsAsync<NotSupportedException>(
                () => buffered.SaveStorageByHashAsync(Hash32(0x05), Hash32(0x06), Array.Empty<byte>()));
            await Assert.ThrowsAsync<NotSupportedException>(
                () => historical.SaveStorageByHashAsync(Hash32(0x05), Hash32(0x06), Array.Empty<byte>()));
        }

        [Fact]
        public async Task Given_AWrapperOverAStoreThatCanWriteFlatRows_When_AFlatRowIsWritten_Then_ItIsDelegatedToTheInnerStore()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            var accountHash = Hash32(0x07);

            await buffered.SaveAccountByHashAsync(accountHash, new Account { Balance = new EvmUInt256(9) });

            Assert.Equal(new EvmUInt256(9), (await inner.GetAccountByHashAsync(accountHash)).Balance);
        }

        private sealed class NonFlatStateStore : IStateStore
        {
            public Task<Account> GetAccountAsync(string address) => Task.FromResult<Account>(null);
            public Task SaveAccountAsync(string address, Account account) => Task.CompletedTask;
            public Task<bool> AccountExistsAsync(string address) => Task.FromResult(false);
            public Task DeleteAccountAsync(string address) => Task.CompletedTask;
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => Task.FromResult(new Dictionary<string, Account>());
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => throw new NotSupportedException();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => Task.FromResult<byte[]>(null);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => Task.CompletedTask;
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => Task.CompletedTask;
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => Task.FromResult(new Dictionary<byte[], byte[]>());
            public Task ClearStorageAsync(string address) => Task.CompletedTask;
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => Task.FromResult<byte[]>(null);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => Task.CompletedTask;
            public Task<IStateSnapshot> CreateSnapshotAsync() => throw new NotSupportedException();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => Task.CompletedTask;
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => Task.CompletedTask;
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
            public Task ClearDirtyTrackingAsync() => Task.CompletedTask;
        }
    }
}
