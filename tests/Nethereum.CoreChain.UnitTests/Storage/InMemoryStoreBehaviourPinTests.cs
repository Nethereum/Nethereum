using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryStoreBehaviourPinTests
    {
        private const string Address = "0x1111111111111111111111111111111111111111";

        private static byte[] Hash(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

        private static Receipt ReceiptWith(byte marker) => Receipt.CreateStatusReceipt(true, new EvmUInt256((ulong)marker), new byte[256], new List<Log>());

        private static async Task SaveReceiptsAsync(InMemoryReceiptStore store, byte[] blockHash, BigInteger block, params byte[] txFills)
        {
            for (int i = 0; i < txFills.Length; i++)
                await store.SaveAsync(ReceiptWith((byte)(50 + i)), Hash(txFills[i]), blockHash, block, i, 21000, null, 1);
        }

        private static BlockStateDiff AccountDiff(BigInteger block, string address, ulong balance) => new BlockStateDiff
        {
            BlockNumber = block,
            AccountDiffs = new List<AccountDiffEntry>
            {
                new AccountDiffEntry
                {
                    Address = address,
                    PreValue = new Account { Balance = balance, Nonce = 1, CodeHash = new byte[32], StateRoot = new byte[32] }
                }
            }
        };

        [Fact]
        public async Task GetByBlockHash_Returns_Receipts_Ordered_By_TxIndex()
        {
            var store = new InMemoryReceiptStore();
            var blockHash = Hash(0xB1);
            await store.SaveAsync(ReceiptWith(102), Hash(3), blockHash, 5, 2, 21000, null, 1);
            await store.SaveAsync(ReceiptWith(100), Hash(1), blockHash, 5, 0, 21000, null, 1);
            await store.SaveAsync(ReceiptWith(101), Hash(2), blockHash, 5, 1, 21000, null, 1);

            var receipts = await store.GetByBlockHashAsync(blockHash);

            Assert.Equal(3, receipts.Count);
            Assert.Equal(new ulong[] { 100, 101, 102 }, receipts.Select(r => (ulong)r.CumulativeGasUsed).ToArray());
        }

        [Fact]
        public async Task GetByBlockNumber_Matches_GetByBlockHash()
        {
            var store = new InMemoryReceiptStore();
            var blockHash = Hash(0xB2);
            await SaveReceiptsAsync(store, blockHash, 9, 0x10, 0x11, 0x12);

            var byHash = await store.GetByBlockHashAsync(blockHash);
            var byNumber = await store.GetByBlockNumberAsync(9);

            Assert.Equal(3, byNumber.Count);
            Assert.Equal(byHash.Select(r => (ulong)r.CumulativeGasUsed), byNumber.Select(r => (ulong)r.CumulativeGasUsed));
            Assert.Empty(await store.GetByBlockNumberAsync(10));
        }

        [Fact]
        public async Task GetByBlockHash_Null_Throws()
        {
            var store = new InMemoryReceiptStore();

            await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetByBlockHashAsync(null));
        }

        [Fact]
        public async Task SaveBlockDiff_Replaces_Prior_Entries_For_Same_Block()
        {
            var store = new InMemoryStateDiffStore();
            await store.SaveBlockDiffAsync(AccountDiff(10, Address, 1));
            await store.SaveBlockDiffAsync(AccountDiff(10, "0x2222222222222222222222222222222222222222", 2));

            var diff = await store.GetBlockDiffAsync(10);
            var (foundFirst, _) = await store.GetFirstAccountPreValueAfterBlockAsync(Address, 9);
            var (foundSecond, second) = await store.GetFirstAccountPreValueAfterBlockAsync("0x2222222222222222222222222222222222222222", 9);

            Assert.Single(diff.AccountDiffs);
            Assert.False(foundFirst);
            Assert.True(foundSecond);
            Assert.Equal(2, second.Balance);
            Assert.Equal((BigInteger)10, await store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)10, await store.GetNewestDiffBlockAsync());
        }

        [Fact]
        public async Task DeleteDiffsAbove_Updates_Bounds()
        {
            var store = new InMemoryStateDiffStore();
            foreach (var block in new[] { 5, 10, 15 })
                await store.SaveBlockDiffAsync(AccountDiff(block, Address, (ulong)block));

            await store.DeleteDiffsAboveBlockAsync(10);

            Assert.Equal((BigInteger)5, await store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)10, await store.GetNewestDiffBlockAsync());
            Assert.Null(await store.GetBlockDiffAsync(15));
        }

        [Fact]
        public async Task DeleteBlockDiff_Missing_Block_Leaves_Bounds()
        {
            var store = new InMemoryStateDiffStore();
            await store.SaveBlockDiffAsync(AccountDiff(5, Address, 5));
            await store.SaveBlockDiffAsync(AccountDiff(15, Address, 15));

            await store.DeleteBlockDiffAsync(10);

            Assert.Equal((BigInteger)5, await store.GetOldestDiffBlockAsync());
            Assert.Equal((BigInteger)15, await store.GetNewestDiffBlockAsync());
            Assert.NotNull(await store.GetBlockDiffAsync(5));
        }

        [Fact]
        public void RewindToCheckpoint_And_Reset_Behave_As_Before()
        {
            var store = new InMemoryChainMetadataStore();
            store.SaveCheckpoint(10, Hash(0x0A), Hash(0xA0));
            store.SaveCheckpoint(20, Hash(0x14), Hash(0xB0));
            store.Commit(25, Hash(0xC0));
            store.SetLastFetchedHeader(30);
            store.SetLastFetchedBody(28);

            Assert.Equal(10UL, store.GetNearestCheckpointAtOrBefore(15)?.BlockNumber);
            Assert.Null(store.GetNearestCheckpointAtOrBefore(9));

            var rewound = store.RewindToCheckpointAtOrBefore(22);

            Assert.Equal(20UL, rewound.BlockNumber);
            Assert.Equal(20UL, store.GetLastBlock());
            Assert.Equal(20UL, store.GetLastFetchedHeader());
            Assert.Equal(20UL, store.GetLastFetchedBody());
            Assert.Throws<InvalidOperationException>(() => store.RewindToCheckpointAtOrBefore(5));

            store.ResetForStateRebuild();

            Assert.Equal(0UL, store.GetLastBlock());
            Assert.Null(store.GetLastBlockHash());
            Assert.Equal(0UL, store.GetLatestCheckpoint());
            Assert.Empty(store.ListCheckpointBlockNumbers());
            Assert.False(store.IsGenesisLoaded());
        }
    }
}
