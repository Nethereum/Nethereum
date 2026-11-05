using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStateStoreSnapshotDirtyTrackingTests
    {
        private const string EarlierAccount = "0x1000000000000000000000000000000000000001";
        private const string LaterAccount = "0x2000000000000000000000000000000000000002";

        private static readonly byte[] One = { 1 };

        private static async Task<InMemoryStateStore> StoreWithEarlierAccountDirtyAsync()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(EarlierAccount, new Account { Balance = 1, Nonce = 0 });
            await store.SaveStorageAsync(EarlierAccount, 7, One);
            return store;
        }

        [Fact]
        public async Task Given_AccountsDirtiedBeforeAndDuringASnapshot_When_TheSnapshotIsReverted_Then_OnlyTheEarlierAccountStaysDirty()
        {
            var store = await StoreWithEarlierAccountDirtyAsync();
            var snapshot = await store.CreateSnapshotAsync();
            await store.SaveAccountAsync(LaterAccount, new Account { Balance = 2, Nonce = 0 });
            await store.SaveAccountAsync(EarlierAccount, new Account { Balance = 3, Nonce = 1 });

            await store.RevertSnapshotAsync(snapshot);

            Assert.Equal(new[] { EarlierAccount }, (await store.GetDirtyAccountAddressesAsync()).Select(a => a.ToLowerInvariant()));
            Assert.Null(await store.GetAccountAsync(LaterAccount));
            Assert.Equal(1, (int)(await store.GetAccountAsync(EarlierAccount)).Balance);
        }

        [Fact]
        public async Task Given_StorageSlotsDirtiedBeforeAndDuringASnapshot_When_TheSnapshotIsReverted_Then_OnlyTheEarlierSlotStaysDirty()
        {
            var store = await StoreWithEarlierAccountDirtyAsync();
            var snapshot = await store.CreateSnapshotAsync();
            await store.SaveStorageAsync(EarlierAccount, 8, One);
            await store.SaveStorageAsync(LaterAccount, 9, One);

            await store.RevertSnapshotAsync(snapshot);

            Assert.Equal(new[] { new BigInteger(7) }, await store.GetDirtyStorageSlotsAsync(EarlierAccount));
            Assert.Empty(await store.GetDirtyStorageSlotsAsync(LaterAccount));
            Assert.Null(await store.GetStorageAsync(LaterAccount, 9));
        }

        [Fact]
        public async Task Given_AccountsDirtiedDuringASnapshot_When_TheSnapshotIsCommitted_Then_TheyStayDirty()
        {
            var store = await StoreWithEarlierAccountDirtyAsync();
            var snapshot = await store.CreateSnapshotAsync();
            await store.SaveAccountAsync(LaterAccount, new Account { Balance = 2, Nonce = 0 });
            await store.SaveStorageAsync(LaterAccount, 9, One);

            await store.CommitSnapshotAsync(snapshot);

            Assert.Equal(2, (await store.GetDirtyAccountAddressesAsync()).Count);
            Assert.Equal(new[] { new BigInteger(9) }, await store.GetDirtyStorageSlotsAsync(LaterAccount));
        }
    }
}
