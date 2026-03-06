using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbStateDiffStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateDiffStore _store;

        private const string ADDR1 = "0x1111111111111111111111111111111111111111";
        private const string ADDR2 = "0x2222222222222222222222222222222222222222";

        public RocksDbStateDiffStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_diff_test_{Guid.NewGuid():N}");
            var options = new RocksDbStorageOptions { DatabasePath = _dbPath };
            _manager = new RocksDbManager(options);
            _store = new RocksDbStateDiffStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); }
                catch { }
            }
        }

        private static Account MakeAccount(BigInteger balance, BigInteger nonce = default)
        {
            return new Account
            {
                Balance = balance,
                Nonce = nonce,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            };
        }

        private static BlockStateDiff MakeAccountDiff(BigInteger blockNumber, string address, Account preValue)
        {
            return new BlockStateDiff
            {
                BlockNumber = blockNumber,
                AccountDiffs = new List<AccountDiffEntry>
                {
                    new AccountDiffEntry { Address = address, PreValue = preValue }
                }
            };
        }

        private static BlockStateDiff MakeStorageDiff(BigInteger blockNumber, string address, BigInteger slot, byte[] preValue)
        {
            return new BlockStateDiff
            {
                BlockNumber = blockNumber,
                StorageDiffs = new List<StorageDiffEntry>
                {
                    new StorageDiffEntry { Address = address, Slot = slot, PreValue = preValue }
                }
            };
        }

        [Fact]
        public async Task SaveAndRetrieveAccountDiff()
        {
            var account = MakeAccount(1000, 5);
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, account));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(found);
            Assert.NotNull(preValue);
            Assert.Equal(1000, preValue.Balance);
            Assert.Equal(5, preValue.Nonce);
        }

        [Fact]
        public async Task SaveAndRetrieveNullAccountDiff()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, null));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(found);
            Assert.Null(preValue);
        }

        [Fact]
        public async Task GetAccountDiff_NotFound_WhenNoModification()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(100)));

            var (found, _) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR2, 9);
            Assert.False(found);
        }

        [Fact]
        public async Task GetAccountDiff_NotFound_WhenQueryBlockIsLatest()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(100)));

            var (found, _) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 10);
            Assert.False(found);
        }

        [Fact]
        public async Task GetAccountDiff_FindsClosestBlock()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(5, ADDR1, MakeAccount(100)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(200)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(15, ADDR1, MakeAccount(300)));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 7);
            Assert.True(found);
            Assert.Equal(200, preValue.Balance);
        }

        [Fact]
        public async Task SaveAndRetrieveStorageDiff()
        {
            var value = new byte[] { 0x01, 0x02, 0x03 };
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 42, value));

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 42, 9);
            Assert.True(found);
            Assert.Equal(value, preValue);
        }

        [Fact]
        public async Task SaveAndRetrieveNullStorageDiff()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 42, null));

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 42, 9);
            Assert.True(found);
            Assert.Null(preValue);
        }

        [Fact]
        public async Task GetStorageDiff_NotFound_WhenDifferentSlot()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 42, new byte[] { 0x01 }));

            var (found, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 99, 9);
            Assert.False(found);
        }

        [Fact]
        public async Task GetStorageDiff_FindsClosestBlock()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(5, ADDR1, 1, new byte[] { 0x0A }));
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 1, new byte[] { 0x0B }));
            await _store.SaveBlockDiffAsync(MakeStorageDiff(15, ADDR1, 1, new byte[] { 0x0C }));

            var (found, preValue) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 7);
            Assert.True(found);
            Assert.Equal(new byte[] { 0x0B }, preValue);
        }

        [Fact]
        public async Task MetaBounds_TrackedCorrectly()
        {
            Assert.Null(await _store.GetOldestDiffBlockAsync());
            Assert.Null(await _store.GetNewestDiffBlockAsync());

            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(100)));

            Assert.Equal((BigInteger)10, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)10, await _store.GetNewestDiffBlockAsync());

            await _store.SaveBlockDiffAsync(MakeAccountDiff(5, ADDR2, MakeAccount(200)));
            Assert.Equal((BigInteger)5, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)10, await _store.GetNewestDiffBlockAsync());

            await _store.SaveBlockDiffAsync(MakeAccountDiff(20, ADDR1, MakeAccount(300)));
            Assert.Equal((BigInteger)5, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)20, await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task DeleteDiffsAboveBlock_RemovesHigherBlocks()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(5, ADDR1, MakeAccount(100)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(200)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(15, ADDR1, MakeAccount(300)));

            await _store.DeleteDiffsAboveBlockAsync(10);

            var (found5, pre5) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 4);
            Assert.True(found5);
            Assert.Equal(100, pre5.Balance);

            var (found10, pre10) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(found10);
            Assert.Equal(200, pre10.Balance);

            var (found15, _) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 14);
            Assert.False(found15);

            Assert.Equal((BigInteger)5, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)10, await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task DeleteDiffsBelowBlock_RemovesLowerBlocks()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(5, ADDR1, MakeAccount(100)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(200)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(15, ADDR1, MakeAccount(300)));

            await _store.DeleteDiffsBelowBlockAsync(10);

            // Block 5 diff deleted, but query from 4 finds block 10 (next available)
            var (foundFrom4, preFrom4) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 4);
            Assert.True(foundFrom4);
            Assert.Equal(200, preFrom4.Balance);

            var (found10, pre10) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(found10);
            Assert.Equal(200, pre10.Balance);

            var (found15, pre15) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 14);
            Assert.True(found15);
            Assert.Equal(300, pre15.Balance);

            Assert.Equal((BigInteger)10, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)15, await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task DeleteAllDiffs_MetaBecomesNull()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(100)));

            await _store.DeleteDiffsAboveBlockAsync(0);

            Assert.Null(await _store.GetOldestDiffBlockAsync());
            Assert.Null(await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task MixedAccountAndStorageDiffs_InSameBlock()
        {
            var diff = new BlockStateDiff
            {
                BlockNumber = 10,
                AccountDiffs = new List<AccountDiffEntry>
                {
                    new AccountDiffEntry { Address = ADDR1, PreValue = MakeAccount(500) }
                },
                StorageDiffs = new List<StorageDiffEntry>
                {
                    new StorageDiffEntry { Address = ADDR1, Slot = 1, PreValue = new byte[] { 0xFF } },
                    new StorageDiffEntry { Address = ADDR1, Slot = 2, PreValue = null }
                }
            };

            await _store.SaveBlockDiffAsync(diff);

            var (foundAcc, preAcc) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(foundAcc);
            Assert.Equal(500, preAcc.Balance);

            var (foundS1, preS1) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 9);
            Assert.True(foundS1);
            Assert.Equal(new byte[] { 0xFF }, preS1);

            var (foundS2, preS2) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 2, 9);
            Assert.True(foundS2);
            Assert.Null(preS2);
        }

        [Fact]
        public async Task MultipleAddresses_IsolatedCorrectly()
        {
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR1, MakeAccount(100)));
            await _store.SaveBlockDiffAsync(MakeAccountDiff(10, ADDR2, MakeAccount(200)));

            var (found1, pre1) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, 9);
            Assert.True(found1);
            Assert.Equal(100, pre1.Balance);

            var (found2, pre2) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR2, 9);
            Assert.True(found2);
            Assert.Equal(200, pre2.Balance);
        }

        [Fact]
        public async Task StorageDiffs_DifferentSlots_IsolatedCorrectly()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 1, new byte[] { 0xAA }));
            await _store.SaveBlockDiffAsync(MakeStorageDiff(10, ADDR1, 2, new byte[] { 0xBB }));

            var (found1, pre1) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 9);
            Assert.True(found1);
            Assert.Equal(new byte[] { 0xAA }, pre1);

            var (found2, pre2) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 2, 9);
            Assert.True(found2);
            Assert.Equal(new byte[] { 0xBB }, pre2);
        }

        [Fact]
        public async Task DeleteAbove_AlsoRemovesStorageDiffs()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(5, ADDR1, 1, new byte[] { 0x01 }));
            await _store.SaveBlockDiffAsync(MakeStorageDiff(15, ADDR1, 1, new byte[] { 0x02 }));

            await _store.DeleteDiffsAboveBlockAsync(10);

            var (found5, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 4);
            Assert.True(found5);

            var (found15, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 14);
            Assert.False(found15);
        }

        [Fact]
        public async Task DeleteBelow_AlsoRemovesStorageDiffs()
        {
            await _store.SaveBlockDiffAsync(MakeStorageDiff(5, ADDR1, 1, new byte[] { 0x01 }));
            await _store.SaveBlockDiffAsync(MakeStorageDiff(15, ADDR1, 1, new byte[] { 0x02 }));

            await _store.DeleteDiffsBelowBlockAsync(10);

            // Block 5 diff deleted, but query from 4 finds block 15 (next available)
            var (foundFrom4, preFrom4) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 4);
            Assert.True(foundFrom4);
            Assert.Equal(new byte[] { 0x02 }, preFrom4);

            var (found15, _) = await _store.GetFirstStoragePreValueAfterBlockAsync(ADDR1, 1, 14);
            Assert.True(found15);

            Assert.Equal((BigInteger)15, await _store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)15, await _store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task LargeBlockNumbers_WorkCorrectly()
        {
            var largeBlock = BigInteger.Parse("999999999999");
            await _store.SaveBlockDiffAsync(MakeAccountDiff(largeBlock, ADDR1, MakeAccount(42)));

            var (found, preValue) = await _store.GetFirstAccountPreValueAfterBlockAsync(ADDR1, largeBlock - 1);
            Assert.True(found);
            Assert.Equal(42, preValue.Balance);

            Assert.Equal(largeBlock, await _store.GetOldestDiffBlockAsync());
            Assert.Equal(largeBlock, await _store.GetNewestDiffBlockAsync());
        }
    }
}
