using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStateDiffStoreTests
    {
        private readonly InMemoryStateDiffStore _store = new();

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

            // Block 3 was pruned, so first diff after block 2 is now block 5 (not 3)
            var (found, pre) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 2);
            Assert.True(found);
            Assert.Equal(50, pre.Balance);

            // Block 5 still exists
            var (found5, pre5) = await _store.GetFirstAccountPreValueAfterBlockAsync(
                "0x1111111111111111111111111111111111111111", 4);
            Assert.True(found5);
            Assert.Equal(50, pre5.Balance);

            // Oldest diff is now 5
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
    }
}
