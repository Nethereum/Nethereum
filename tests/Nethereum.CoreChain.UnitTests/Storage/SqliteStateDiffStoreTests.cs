using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class SqliteStateDiffStoreTests : IDisposable
    {
        private readonly SqliteStorageManager _manager;
        private readonly SqliteStateDiffStore _store;

        public SqliteStateDiffStoreTests()
        {
            _manager = new SqliteStorageManager();
            _store = new SqliteStateDiffStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
        }

        private static Account MakeAccount(BigInteger balance, BigInteger nonce = default)
        {
            return new Account { Balance = balance, Nonce = nonce };
        }

        private static BlockStateDiff MakeDiff(BigInteger blockNumber, params (string Address, Account PreValue)[] accounts)
        {
            var diff = new BlockStateDiff { BlockNumber = blockNumber };
            foreach (var (addr, pre) in accounts)
                diff.AccountDiffs.Add(new AccountDiffEntry { Address = addr, PreValue = pre });
            return diff;
        }

        [Fact]
        public async Task SaveAndQuery_SingleBlock_ReturnsPreValue()
        {
            var diff = MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(100)));
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 4);

            Assert.True(found);
            Assert.Equal(100, preValue.Balance);
        }

        [Fact]
        public async Task Query_NoMatchingDiffs_ReturnsNotFound()
        {
            var diff = MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(100)));
            await _store.SaveBlockDiffAsync(diff);

            var (found, _) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 5);

            Assert.False(found);
        }

        [Fact]
        public async Task Query_MultipleBlocks_ReturnsFirstAfterTarget()
        {
            await _store.SaveBlockDiffAsync(MakeDiff(3, ("0x1111111111111111111111111111111111111111", MakeAccount(50))));
            await _store.SaveBlockDiffAsync(MakeDiff(7, ("0x1111111111111111111111111111111111111111", MakeAccount(100))));
            await _store.SaveBlockDiffAsync(MakeDiff(10, ("0x1111111111111111111111111111111111111111", MakeAccount(200))));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 5);

            Assert.True(found);
            Assert.Equal(100, preValue.Balance);
        }

        [Fact]
        public async Task Query_TargetAtBlock0_ReturnsEarliestDiff()
        {
            await _store.SaveBlockDiffAsync(MakeDiff(1, ("0x1111111111111111111111111111111111111111", MakeAccount(0))));
            await _store.SaveBlockDiffAsync(MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(100))));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 0);

            Assert.True(found);
            Assert.Equal(0, preValue.Balance);
        }

        [Fact]
        public async Task Query_DifferentAddresses_DoNotInterfere()
        {
            var addr1 = "0x1111111111111111111111111111111111111111";
            var addr2 = "0x2222222222222222222222222222222222222222";

            await _store.SaveBlockDiffAsync(MakeDiff(5, (addr1, MakeAccount(100))));
            await _store.SaveBlockDiffAsync(MakeDiff(5, (addr2, MakeAccount(200))));

            var (found1, pre1) = await _store.GetFirstAccountPreValueAfterBlockAsync(addr1, 4);
            var (found2, pre2) = await _store.GetFirstAccountPreValueAfterBlockAsync(addr2, 4);

            Assert.True(found1);
            Assert.Equal(100, pre1.Balance);
            Assert.True(found2);
            Assert.Equal(200, pre2.Balance);
        }

        [Fact]
        public async Task StorageDiff_SaveAndQuery_ReturnsPreValue()
        {
            var diff = new BlockStateDiff { BlockNumber = 5 };
            diff.StorageDiffs.Add(new StorageDiffEntry
            {
                Address = "0x1111111111111111111111111111111111111111",
                Slot = 42,
                PreValue = new byte[] { 1, 2, 3 }
            });
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 42, 4);

            Assert.True(found);
            Assert.Equal(new byte[] { 1, 2, 3 }, preValue);
        }

        [Fact]
        public async Task StorageDiff_DifferentSlots_DoNotInterfere()
        {
            var addr = "0x1111111111111111111111111111111111111111";
            var diff = new BlockStateDiff { BlockNumber = 5 };
            diff.StorageDiffs.Add(new StorageDiffEntry { Address = addr, Slot = 1, PreValue = new byte[] { 0x0A } });
            diff.StorageDiffs.Add(new StorageDiffEntry { Address = addr, Slot = 2, PreValue = new byte[] { 0x0B } });
            await _store.SaveBlockDiffAsync(diff);

            var (found1, val1) = await _store.GetFirstStoragePreValueAfterBlockAsync(addr, 1, 4);
            var (found2, val2) = await _store.GetFirstStoragePreValueAfterBlockAsync(addr, 2, 4);

            Assert.True(found1);
            Assert.Equal(new byte[] { 0x0A }, val1);
            Assert.True(found2);
            Assert.Equal(new byte[] { 0x0B }, val2);
        }

        [Fact]
        public async Task DeleteAbove_RemovesFutureBlocks()
        {
            await _store.SaveBlockDiffAsync(MakeDiff(3, ("0x1111111111111111111111111111111111111111", MakeAccount(30))));
            await _store.SaveBlockDiffAsync(MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(50))));
            await _store.SaveBlockDiffAsync(MakeDiff(8, ("0x1111111111111111111111111111111111111111", MakeAccount(80))));

            await _store.DeleteDiffsAboveBlockAsync(5);

            var (found8, _) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 7);
            Assert.False(found8);

            var (found3, pre3) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 2);
            Assert.True(found3);
            Assert.Equal(30, pre3.Balance);
        }

        [Fact]
        public async Task DeleteBelow_RemovesOldBlocks()
        {
            await _store.SaveBlockDiffAsync(MakeDiff(3, ("0x1111111111111111111111111111111111111111", MakeAccount(30))));
            await _store.SaveBlockDiffAsync(MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(50))));
            await _store.SaveBlockDiffAsync(MakeDiff(8, ("0x1111111111111111111111111111111111111111", MakeAccount(80))));

            await _store.DeleteDiffsBelowBlockAsync(5);

            var (found, pre) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 2);
            Assert.True(found);
            Assert.Equal(50, pre.Balance);

            var (found5, pre5) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 4);
            Assert.True(found5);
            Assert.Equal(50, pre5.Balance);

            var oldest = await _store.GetOldestDiffBlockAsync();
            Assert.Equal(5, oldest);
        }

        [Fact]
        public async Task Bounds_TrackCorrectly()
        {
            Assert.Null(await _store.GetOldestDiffBlockAsync());
            Assert.Null(await _store.GetNewestDiffBlockAsync());

            await _store.SaveBlockDiffAsync(MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(50))));
            await _store.SaveBlockDiffAsync(MakeDiff(10, ("0x1111111111111111111111111111111111111111", MakeAccount(100))));

            Assert.Equal(5, await _store.GetOldestDiffBlockAsync());
            Assert.Equal(10, await _store.GetNewestDiffBlockAsync());

            await _store.DeleteDiffsBelowBlockAsync(8);
            Assert.Equal(10, await _store.GetOldestDiffBlockAsync());
        }

        [Fact]
        public async Task NullPreValue_StoredAndRetrieved()
        {
            var diff = MakeDiff(5, ("0x1111111111111111111111111111111111111111", null));
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 4);

            Assert.True(found);
            Assert.Null(preValue);
        }

        [Fact]
        public async Task NullStoragePreValue_StoredAndRetrieved()
        {
            var diff = new BlockStateDiff { BlockNumber = 5 };
            diff.StorageDiffs.Add(new StorageDiffEntry
            {
                Address = "0x1111111111111111111111111111111111111111",
                Slot = 42,
                PreValue = null
            });
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 42, 4);

            Assert.True(found);
            Assert.Null(preValue);
        }

        [Fact]
        public async Task DeleteAbove_AlsoDeletesStorageDiffs()
        {
            var addr = "0x1111111111111111111111111111111111111111";
            var diff = new BlockStateDiff { BlockNumber = 10 };
            diff.StorageDiffs.Add(new StorageDiffEntry { Address = addr, Slot = 1, PreValue = new byte[] { 0xFF } });
            await _store.SaveBlockDiffAsync(diff);

            await _store.DeleteDiffsAboveBlockAsync(5);

            var (found, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(addr, 1, 9);
            Assert.False(found);
        }

        [Fact]
        public async Task DeleteBelow_AlsoDeletesStorageDiffs()
        {
            var addr = "0x1111111111111111111111111111111111111111";
            var diff = new BlockStateDiff { BlockNumber = 3 };
            diff.StorageDiffs.Add(new StorageDiffEntry { Address = addr, Slot = 1, PreValue = new byte[] { 0xFF } });
            await _store.SaveBlockDiffAsync(diff);

            await _store.DeleteDiffsBelowBlockAsync(5);

            var (found, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(addr, 1, 2);
            Assert.False(found);
        }

        [Fact]
        public async Task DeleteAll_BoundsResetToNull()
        {
            await _store.SaveBlockDiffAsync(MakeDiff(5, ("0x1111111111111111111111111111111111111111", MakeAccount(50))));
            await _store.SaveBlockDiffAsync(MakeDiff(10, ("0x1111111111111111111111111111111111111111", MakeAccount(100))));

            await _store.DeleteDiffsBelowBlockAsync(20);

            Assert.Null(await _store.GetOldestDiffBlockAsync());
            Assert.Null(await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task MultipleAccountsInSameBlock_AllStored()
        {
            var addr1 = "0x1111111111111111111111111111111111111111";
            var addr2 = "0x2222222222222222222222222222222222222222";

            var diff = new BlockStateDiff { BlockNumber = 5 };
            diff.AccountDiffs.Add(new AccountDiffEntry { Address = addr1, PreValue = MakeAccount(100) });
            diff.AccountDiffs.Add(new AccountDiffEntry { Address = addr2, PreValue = MakeAccount(200) });
            await _store.SaveBlockDiffAsync(diff);

            var (found1, pre1) = await _store.GetFirstAccountPreValueAfterBlockAsync(addr1, 4);
            var (found2, pre2) = await _store.GetFirstAccountPreValueAfterBlockAsync(addr2, 4);

            Assert.True(found1);
            Assert.Equal(100, pre1.Balance);
            Assert.True(found2);
            Assert.Equal(200, pre2.Balance);
        }

        [Fact]
        public async Task AccountWithNonce_PreservedThroughRoundtrip()
        {
            var account = new Account { Balance = 12345, Nonce = 42 };
            var diff = MakeDiff(5, ("0x1111111111111111111111111111111111111111", account));
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 4);

            Assert.True(found);
            Assert.Equal(12345, preValue.Balance);
            Assert.Equal(42, preValue.Nonce);
        }

        [Fact]
        public async Task LargeSlotNumber_HandledCorrectly()
        {
            var addr = "0x1111111111111111111111111111111111111111";
            var largeSlot = BigInteger.Pow(2, 200);
            var diff = new BlockStateDiff { BlockNumber = 5 };
            diff.StorageDiffs.Add(new StorageDiffEntry { Address = addr, Slot = largeSlot, PreValue = new byte[] { 0xAB } });
            await _store.SaveBlockDiffAsync(diff);

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(addr, largeSlot, 4);

            Assert.True(found);
            Assert.Equal(new byte[] { 0xAB }, preValue);
        }
    }
}
