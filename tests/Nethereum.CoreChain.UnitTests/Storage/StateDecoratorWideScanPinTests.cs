using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class StateDecoratorWideScanPinTests
    {
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrC = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string AddrD = "0xdddddddddddddddddddddddddddddddddddddddd";

        public static IEnumerable<object[]> Decorators()
        {
            yield return new object[] { "wrapper" };
            yield return new object[] { "buffered" };
        }

        private static async Task<(IStateStore Decorator, InMemoryStateStore Inner)> BuildAsync(string kind)
        {
            var inner = new InMemoryStateStore();
            await inner.SaveAccountAsync(AddrA, new Account { Balance = 1 });
            await inner.SaveAccountAsync(AddrB, new Account { Balance = 2 });
            await inner.SaveAccountAsync(AddrC, new Account { Balance = 3 });
            await inner.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await inner.SaveStorageAsync(AddrA, 2, new byte[] { 0x02 });
            await inner.SaveStorageAsync(AddrA, 3, new byte[] { 0x03 });
            await inner.SaveStorageAsync(AddrB, 1, new byte[] { 0x0B });

            if (kind == "wrapper") return (new ReadOnlyStateStoreWrapper(inner), inner);
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();
            return (buffered, inner);
        }

        private static Dictionary<BigInteger, byte[]> BySlot(Dictionary<byte[], byte[]> all)
        {
            var result = new Dictionary<BigInteger, byte[]>();
            foreach (var slot in new BigInteger[] { 1, 2, 3, 4, 5 })
                if (all.TryGetValue(StateKeys.StorageSlotKey(slot), out var value)) result[slot] = value;
            Assert.Equal(result.Count, all.Count);
            return result;
        }

        [Theory]
        [MemberData(nameof(Decorators))]
        public async Task Wrapper_And_Buffered_GetAllStorage_Merge_Cleared_And_Deleted_Slots(string kind)
        {
            var (store, inner) = await BuildAsync(kind);

            await store.SaveStorageAsync(AddrA, 2, new byte[] { 0x00 });
            await store.SaveStorageAsync(AddrA, 4, new byte[] { 0x04 });
            await store.SaveStorageAsync(AddrA, 3, new byte[] { 0x33 });
            await store.ClearStorageAsync(AddrB);
            await store.SaveStorageAsync(AddrB, 5, new byte[] { 0x55 });

            var a = BySlot(await store.GetAllStorageAsync(AddrA));
            var b = BySlot(await store.GetAllStorageAsync(AddrB));
            var untouched = BySlot(await store.GetAllStorageAsync(AddrC));

            Assert.Equal(new BigInteger[] { 1, 3, 4 }, a.Keys.OrderBy(k => k).ToArray());
            Assert.Equal(new byte[] { 0x33 }, a[3]);
            Assert.Equal(new BigInteger[] { 5 }, b.Keys.ToArray());
            Assert.Empty(untouched);
            Assert.Equal(new byte[] { 0x02 }, await inner.GetStorageAsync(AddrA, 2));
        }

        [Theory]
        [MemberData(nameof(Decorators))]
        public async Task StreamAccounts_Skips_Deleted_And_Deduplicates_Overlay(string kind)
        {
            var (store, _) = await BuildAsync(kind);

            await store.SaveAccountAsync(AddrA, new Account { Balance = 100 });
            await store.SaveAccountAsync(AddrD, new Account { Balance = 4 });
            await store.DeleteAccountAsync(AddrB);

            var streamed = new List<KeyValuePair<string, Account>>();
            await foreach (var kv in store.StreamAccountsAsync()) streamed.Add(kv);
            var all = await store.GetAllAccountsAsync();

            var keys = streamed.Select(kv => kv.Key.ToLowerInvariant()).OrderBy(k => k).ToArray();
            Assert.Equal(new[] { AddrA, AddrC, AddrD }, keys);
            Assert.Equal((EvmUInt256)100, streamed.Single(kv => kv.Key.ToLowerInvariant() == AddrA).Value.Balance);
            Assert.Equal(keys, all.Keys.Select(k => k.ToLowerInvariant()).OrderBy(k => k).ToArray());
            Assert.Equal((EvmUInt256)100, all.Single(kv => kv.Key.ToLowerInvariant() == AddrA).Value.Balance);
        }

        [Theory]
        [MemberData(nameof(Decorators))]
        public async Task Given_OverlayAccountWrites_When_AccountExistsIsAsked_Then_OverlayAnswersBeforeTheInnerStore(string kind)
        {
            var (store, _) = await BuildAsync(kind);

            await store.DeleteAccountAsync(AddrA);
            await store.SaveAccountAsync(AddrD, new Account { Balance = 4 });

            Assert.False(await store.AccountExistsAsync(AddrA));
            Assert.True(await store.AccountExistsAsync(AddrB));
            Assert.True(await store.AccountExistsAsync(AddrD));
            Assert.False(await store.AccountExistsAsync("0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"));
        }

        [Theory]
        [MemberData(nameof(Decorators))]
        public async Task Given_CodeSavedInTheOverlay_When_ReadByHash_Then_OverlayCodeIsReturnedAndNullHashFallsThrough(string kind)
        {
            var (store, inner) = await BuildAsync(kind);
            var hash = new Sha3Keccack().CalculateHash(new byte[] { 0x60, 0x00 });
            await inner.SaveCodeAsync(new byte[] { 9, 9 }, new byte[] { 0x09 });

            await store.SaveCodeAsync(hash, new byte[] { 0x60, 0x00 });

            Assert.Equal(new byte[] { 0x60, 0x00 }, await store.GetCodeAsync(hash));
            Assert.Equal(new byte[] { 0x09 }, await store.GetCodeAsync(new byte[] { 9, 9 }));
            Assert.Null(await store.GetCodeAsync(new byte[] { 7, 7 }));
        }

        [Fact]
        public async Task Given_ASqliteStateStore_When_AnEmptyNullOrAllZeroValueIsWritten_Then_TheSlotIsRemoved()
        {
            using var manager = new SqliteStorageManager(null, deleteOnDispose: true);
            var store = new SqliteStateStore(manager);

            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await store.SaveStorageAsync(AddrA, 2, new byte[] { 0x02 });
            await store.SaveStorageAsync(AddrA, 3, new byte[] { 0x03 });
            await store.SaveStorageAsync(AddrA, 1, new byte[] { 0x00, 0x00 });
            await store.SaveStorageAsync(AddrA, 2, null);
            await store.SaveStorageAsync(AddrA, 4, new byte[0]);
            await store.SaveStorageByKeccakAsync(AddrA, StateKeys.StorageSlotKey(3), new byte[] { 0x00 });

            Assert.Empty(await store.GetAllStorageAsync(AddrA));
            await store.SaveStorageAsync(AddrA, 5, new byte[] { 0x00, 0x05 });
            Assert.Equal(new byte[] { 0x05 }, (await store.GetStorageAsync(AddrA, 5)).SkipWhile(b => b == 0).ToArray());
        }

        [Fact]
        public async Task Given_TheInMemoryStateStoreAndTheOverlay_When_AllZeroValuesAreWritten_Then_TheSlotIsAbsent()
        {
            var inner = new InMemoryStateStore();
            var wrapper = new ReadOnlyStateStoreWrapper(inner);

            await inner.SaveStorageAsync(AddrA, 1, new byte[] { 0x01 });
            await inner.SaveStorageAsync(AddrA, 1, new byte[] { 0x00, 0x00, 0x00 });
            await wrapper.SaveStorageAsync(AddrA, 2, new byte[] { 0x02 });
            await wrapper.SaveStorageAsync(AddrA, 2, new byte[] { 0x00 });
            await wrapper.SaveStorageAsync(AddrA, 3, new byte[] { 0x03 });
            await wrapper.SaveStorageAsync(AddrA, 3, new byte[0]);

            Assert.Empty(await inner.GetAllStorageAsync(AddrA));
            Assert.Empty(await wrapper.GetAllStorageAsync(AddrA));
        }

        [Fact]
        public async Task SaveAccount_And_DeleteAccount_Journal_PreValue_Once()
        {
            var inner = new InMemoryStateStore();
            var store = new HistoricalStateStore(inner, new InMemoryStateDiffStore(), HistoricalStateOptions.FullArchive);
            await store.SaveAccountAsync(AddrA, new Account { Balance = 100 });

            store.SetCurrentBlockNumber(1);
            await store.SaveAccountAsync(AddrA, new Account { Balance = 200 });
            await store.SaveAccountAsync(AddrA, new Account { Balance = 300 });
            await store.SaveAccountAsync(AddrB, new Account { Balance = 7 });
            await store.DeleteAccountAsync(AddrB);
            await store.DeleteAccountAsync(AddrA);
            await store.ClearCurrentBlockNumberAsync();

            Assert.Equal((EvmUInt256)100, (await store.GetAccountAtBlockAsync(AddrA, 0)).Balance);
            Assert.Null(await store.GetAccountAtBlockAsync(AddrB, 0));
            Assert.Null(await store.GetAccountAsync(AddrA));
        }

        [Fact]
        public async Task Given_TwoSnapshotsOfAReadOnlyStateReaderStore_When_Created_Then_IdsIncrementAndRevertDoesNothing()
        {
            var reader = new StateStoreNodeDataService(new InMemoryStateStore());
            var store = new ReadOnlyStateReaderStore(reader);

            var first = await store.CreateSnapshotAsync();
            var second = await store.CreateSnapshotAsync();
            first.SetAccount(AddrA, new Account());
            await store.RevertSnapshotAsync(second);
            await store.CommitSnapshotAsync(first);

            Assert.Equal(1, first.SnapshotId);
            Assert.Equal(2, second.SnapshotId);
            first.Dispose();
        }

        [Fact]
        public async Task Given_AHistoricalStateStoreReadAdapter_When_ASnapshotIsCreated_Then_ItsIdIsZero()
        {
            var live = new InMemoryStateStore();
            var historical = new HistoricalStateStore(live);
            var adapter = new HistoricalStateStoreReadAdapter(historical, live, 5);

            var snapshot = await adapter.CreateSnapshotAsync();
            snapshot.SetStorage(AddrA, 1, new byte[] { 1 });
            await adapter.RevertSnapshotAsync(snapshot);
            await adapter.CommitSnapshotAsync(snapshot);

            Assert.Equal(0, snapshot.SnapshotId);
            snapshot.Dispose();
        }

        [Fact]
        public async Task Given_TheEmptyCodeHashOrNull_When_CodeIsRead_Then_AllThreeStateServicesReturnNull()
        {
            var live = new InMemoryStateStore();
            await live.SaveAccountAsync(AddrA, new Account { Balance = 1, CodeHash = DefaultValues.EMPTY_DATA_HASH });
            await live.SaveAccountAsync(AddrB, new Account { Balance = 1, CodeHash = null });
            var historical = new HistoricalStateStore(live);
            var storeService = new StateStoreNodeDataService(live);
            var historicalService = new HistoricalNodeDataService(historical, live, null, 0);
            var readerStore = new ReadOnlyStateReaderStore(storeService);

            Assert.Null(await storeService.GetCodeAsync(AddrA));
            Assert.Null(await storeService.GetCodeAsync(AddrB));
            Assert.Null(await historicalService.GetCodeAsync(AddrA));
            Assert.Null(await historicalService.GetCodeAsync(AddrB));
            Assert.Null(await readerStore.GetCodeAsync(DefaultValues.EMPTY_DATA_HASH));
            Assert.Null(await readerStore.GetCodeAsync(null));
            Assert.Null(await readerStore.GetAccountAsync(AddrC));
            Assert.NotNull(await readerStore.GetAccountAsync(AddrA));
        }
    }
}
