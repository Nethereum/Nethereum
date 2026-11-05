using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStateStoreAddressSpellingTests
    {
        private const string Checksummed = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private static readonly EvmAddress Address = EvmAddress.FromHex(Checksummed);
        private static readonly byte[] Value = { 7 };

        [Fact]
        public async Task Given_AnAccountSavedByEvmAddress_When_ReadByEveryAddressSpelling_Then_ItIsTheSameAccount()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(Address, new Account { Balance = 5, Nonce = 3 });

            foreach (var spelling in new[] { Checksummed, Checksummed.ToLowerInvariant(), Checksummed.ToUpperInvariant().Replace("0X", "0x") })
            {
                Assert.Equal(5, (int)(await store.GetAccountAsync(spelling)).Balance);
                Assert.True(await store.AccountExistsAsync(spelling));
            }
            Assert.Equal(3, (int)(await store.GetAccountAsync(Address)).Nonce);
            Assert.True(await store.AccountExistsAsync(Address));
            Assert.Equal(new[] { Checksummed.ToLowerInvariant() }, await store.GetDirtyAccountAddressesAsync());
        }

        [Fact]
        public async Task Given_AnAccountSavedByString_When_ReadByEvmAddress_Then_ItIsTheSameAccount()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(Checksummed, new Account { Balance = 9, Nonce = 1 });

            Assert.Equal(9, (int)(await store.GetAccountAsync(Address)).Balance);
        }

        [Fact]
        public async Task Given_AnAccountDeletedByEvmAddress_When_ItIsSavedAgain_Then_ItIsReadBack()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(Address, new Account { Balance = 5, Nonce = 3 });
            await store.DeleteAccountAsync(Address);
            Assert.Null(await store.GetAccountAsync(Address));
            Assert.Null(await store.GetAccountAsync(Checksummed));

            await store.SaveAccountAsync(Address, new Account { Balance = 6, Nonce = 4 });

            Assert.Equal(6, (int)(await store.GetAccountAsync(Checksummed)).Balance);
            Assert.Equal(4, (int)(await store.GetAccountAsync(Address)).Nonce);
        }

        [Fact]
        public async Task Given_StorageSavedByEvmAddress_When_ReadByStringAndCleared_Then_BothSpellingsAgree()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(Address, new Account { Balance = 1, Nonce = 1 });
            await store.SaveStorageAsync(Address, 4, Value);

            Assert.Equal(Value, await store.GetStorageAsync(Checksummed, 4));
            Assert.Equal(new[] { new BigInteger(4) }, await store.GetDirtyStorageSlotsAsync(Checksummed));

            await store.ClearStorageAsync(Address);

            Assert.Null(await store.GetStorageAsync(Checksummed, 4));
            Assert.Null(await store.GetStorageAsync(Address, 4));
        }
    }
}
