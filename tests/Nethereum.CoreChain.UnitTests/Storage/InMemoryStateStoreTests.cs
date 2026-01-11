using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStateStoreTests
    {
        [Fact]
        public async Task SaveAndGetAccount_ShouldWork()
        {
            var store = new InMemoryStateStore();
            var account = new Account { Balance = 1000, Nonce = 5 };

            await store.SaveAccountAsync("0x1234", account);
            var retrieved = await store.GetAccountAsync("0x1234");

            Assert.NotNull(retrieved);
            Assert.Equal(1000, retrieved.Balance);
            Assert.Equal(5, retrieved.Nonce);
        }

        [Fact]
        public async Task AccountExists_ShouldReturnCorrectValue()
        {
            var store = new InMemoryStateStore();
            var account = new Account { Balance = 100 };

            await store.SaveAccountAsync("0xABCD", account);

            Assert.True(await store.AccountExistsAsync("0xABCD"));
            Assert.False(await store.AccountExistsAsync("0xFFFF"));
        }

        [Fact]
        public async Task DeleteAccount_ShouldRemoveAccount()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync("0x1234", new Account { Balance = 100 });

            await store.DeleteAccountAsync("0x1234");

            Assert.False(await store.AccountExistsAsync("0x1234"));
        }

        [Fact]
        public async Task SaveAndGetStorage_ShouldWork()
        {
            var store = new InMemoryStateStore();
            var value = new byte[] { 0x01, 0x02, 0x03 };

            await store.SaveStorageAsync("0x1234", BigInteger.One, value);
            var retrieved = await store.GetStorageAsync("0x1234", BigInteger.One);

            Assert.Equal(value, retrieved);
        }

        [Fact]
        public async Task SaveAndGetCode_ShouldWork()
        {
            var store = new InMemoryStateStore();
            var codeHash = new byte[] { 0xAA, 0xBB, 0xCC };
            var code = new byte[] { 0x60, 0x80, 0x60, 0x40 };

            await store.SaveCodeAsync(codeHash, code);
            var retrieved = await store.GetCodeAsync(codeHash);

            Assert.Equal(code, retrieved);
        }

        [Fact]
        public async Task Snapshot_ShouldPreserveStateOnRevert()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync("0x1234", new Account { Balance = 100 });

            var snapshot = await store.CreateSnapshotAsync();

            await store.SaveAccountAsync("0x1234", new Account { Balance = 500 });
            var afterChange = await store.GetAccountAsync("0x1234");
            Assert.Equal(500, afterChange.Balance);

            await store.RevertSnapshotAsync(snapshot);
            var afterRevert = await store.GetAccountAsync("0x1234");
            Assert.Equal(100, afterRevert.Balance);
        }

        [Fact]
        public async Task GetAllStorage_ShouldReturnAllSlots()
        {
            var store = new InMemoryStateStore();

            await store.SaveStorageAsync("0x1234", BigInteger.Zero, new byte[] { 0x01 });
            await store.SaveStorageAsync("0x1234", BigInteger.One, new byte[] { 0x02 });
            await store.SaveStorageAsync("0x1234", new BigInteger(2), new byte[] { 0x03 });

            var allStorage = await store.GetAllStorageAsync("0x1234");

            Assert.Equal(3, allStorage.Count);
        }

        [Fact]
        public async Task AddressNormalization_ShouldBeCaseInsensitive()
        {
            var store = new InMemoryStateStore();

            await store.SaveAccountAsync("0xABCD", new Account { Balance = 100 });
            var retrieved = await store.GetAccountAsync("0xabcd");

            Assert.NotNull(retrieved);
            Assert.Equal(100, retrieved.Balance);
        }
    }
}
