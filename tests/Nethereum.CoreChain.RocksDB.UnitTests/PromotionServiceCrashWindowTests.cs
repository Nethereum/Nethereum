using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionServiceCrashWindowTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotioncrash_{Guid.NewGuid():N}");

        public PromotionServiceCrashWindowTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private sealed class Rig
        {
            public RocksDbHotBlockWindowStore Hot;
            public RocksDbReceiptStore HotReceipts;
            public RocksDbBlockStore HistoryBlocks;
            public RocksDbTransactionStore HistoryTransactions;
            public RocksDbReceiptStore HistoryReceipts;
            public RocksDbChainMetadataStore Metadata;
            public RocksDbPromotionService Promotion;
        }

        private static Rig BuildRig(RocksDbManager manager, ulong maxHistoryBlocks)
        {
            var hot = new RocksDbHotBlockWindowStore(manager, windowSize: 64, evictOnWrite: false);
            var hotReceipts = new RocksDbReceiptStore(manager, receiptBodyCf: RocksDbManager.CF_HOT_RECEIPT_BODY,
                txHashIndexCf: RocksDbManager.CF_HOT_TX_HASH_INDEX, writeTxHashIndex: false);
            var historyBlocks = new RocksDbBlockStore(manager);
            var historyTransactions = new RocksDbTransactionStore(manager, historyBlocks);
            var historyReceipts = new RocksDbReceiptStore(manager, historyBlocks);
            var historyBlockAccessLists = new RocksDbBlockAccessListStore(manager);
            var metadata = new RocksDbChainMetadataStore(manager);
            var promotion = new RocksDbPromotionService(
                manager, hot, historyBlocks, historyTransactions, historyReceipts, historyBlockAccessLists,
                metadata, maxHistoryBlocks);
            return new Rig
            {
                Hot = hot, HotReceipts = hotReceipts, HistoryBlocks = historyBlocks,
                HistoryTransactions = historyTransactions, HistoryReceipts = historyReceipts,
                Metadata = metadata, Promotion = promotion,
            };
        }

        private static async Task FollowIntoHotAsync(Rig rig, TestBlock block)
        {
            rig.Hot.WriteHeader(block.Header, block.Hash);
            rig.Hot.WriteTransactions((ulong)block.Number, block.Transactions);
            await rig.HotReceipts.SaveManyAsync(block.Hash, block.Number, block.Receipts).ConfigureAwait(false);
        }

        [Fact]
        public async Task Follow_PromotesAgedBlock_AppendsHistory_RemovesFromHot()
        {
            var dir = Path.Combine(_root, "happy");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var rig = BuildRig(manager, maxHistoryBlocks: 3);

            for (long n = 1; n <= 5; n++)
            {
                var block = MakeBlock(n);
                await FollowIntoHotAsync(rig, block);
                rig.Metadata.CommitDurableState((ulong)block.Number, block.Hash);
                rig.Promotion.PromoteDurableBlocks((ulong)n);
            }

            Assert.Equal(2UL, rig.Metadata.GetPromotionCursor());

            Assert.False(rig.Hot.ContainsBlock(1));
            Assert.False(rig.Hot.ContainsBlock(2));
            Assert.True(rig.Hot.ContainsBlock(3));
            Assert.True(rig.Hot.ContainsBlock(4));
            Assert.True(rig.Hot.ContainsBlock(5));

            foreach (var n in new BigInteger[] { 1, 2 })
            {
                Assert.NotNull(await rig.HistoryBlocks.GetByNumberAsync(n));
                Assert.Equal(2, (await rig.HistoryTransactions.GetByBlockNumberAsync(n)).Count);
                Assert.Equal(2, (await rig.HistoryReceipts.GetByBlockNumberAsync(n)).Count);
            }
        }

        [Fact]
        public void PromoteDurableBlocks_BlockMissingFromHot_ThrowsAndLeavesCursorAndHistoryUntouched()
        {
            var dir = Path.Combine(_root, "missing-block");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var rig = BuildRig(manager, maxHistoryBlocks: 3);

            var ex = Assert.Throws<InvalidOperationException>(() => rig.Promotion.PromoteDurableBlocks(4));
            Assert.Contains("block 1", ex.Message);

            Assert.Equal(0UL, rig.Metadata.GetPromotionCursor());
            Assert.Null(rig.HistoryBlocks.GetByNumberAsync(1).GetAwaiter().GetResult());
        }

        [Fact]
        public async Task Crash_AfterCursorBeforeHotDelete_Boot_NoOrphanNoLoss()
        {
            var dir = Path.Combine(_root, "cw-c");
            Directory.CreateDirectory(dir);

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var rig = BuildRig(manager, maxHistoryBlocks: 3);
                var block = MakeBlock(1);
                await FollowIntoHotAsync(rig, block);

                await rig.HistoryBlocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
                await rig.HistoryTransactions.SaveManyAsync(block.Hash, block.Number, block.Transactions).ConfigureAwait(false);
                await rig.HistoryReceipts.SaveManyAsync(block.Hash, block.Number, block.Receipts).ConfigureAwait(false);

                using (var batch = manager.CreateWriteBatch())
                {
                    rig.Metadata.AddPromotionCursorToBatch(batch, 1);
                    manager.Write(batch);
                }

                Assert.Equal(1UL, rig.Metadata.GetPromotionCursor());
                Assert.True(rig.Hot.ContainsBlock(1), "precondition: hot still holds the orphaned block before boot reconciliation runs");
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var rig = BuildRig(manager, maxHistoryBlocks: 3);
                rig.Promotion.ReconcilePromotionOnBoot(durableHead: 0);

                Assert.False(rig.Hot.ContainsBlock(1), "boot sweep must remove the orphaned hot copy");
                Assert.Equal(1UL, rig.Metadata.GetPromotionCursor());
                Assert.NotNull(await rig.HistoryBlocks.GetByNumberAsync(1));
                Assert.Equal(2, (await rig.HistoryTransactions.GetByBlockNumberAsync(1)).Count);
                Assert.Equal(2, (await rig.HistoryReceipts.GetByBlockNumberAsync(1)).Count);
            }
        }

        [Fact]
        public async Task Crash_AfterHeadDurableBeforePromotion_Boot_ReDrivesPromotion()
        {
            var dir = Path.Combine(_root, "cw-b");
            Directory.CreateDirectory(dir);

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var rig = BuildRig(manager, maxHistoryBlocks: 3);
                rig.Promotion.ReconcilePromotionOnBoot(rig.Metadata.GetDurableStateBlock());
                Assert.Equal(0UL, rig.Metadata.GetPromotionCursor());

                TestBlock last = null;
                for (long n = 1; n <= 4; n++)
                {
                    var block = MakeBlock(n);
                    await FollowIntoHotAsync(rig, block);
                    last = block;
                }
                rig.Metadata.CommitDurableState((ulong)last.Number, last.Hash);

                Assert.Equal(0UL, rig.Metadata.GetPromotionCursor());
                Assert.True(rig.Hot.ContainsBlock(1), "precondition: block 1 is still only in hot before boot reconciliation runs");
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var rig = BuildRig(manager, maxHistoryBlocks: 3);
                rig.Promotion.ReconcilePromotionOnBoot(rig.Metadata.GetDurableStateBlock());

                Assert.Equal(1UL, rig.Metadata.GetPromotionCursor());
                Assert.False(rig.Hot.ContainsBlock(1));
                Assert.True(rig.Hot.ContainsBlock(2));
                Assert.True(rig.Hot.ContainsBlock(3));
                Assert.True(rig.Hot.ContainsBlock(4));
                Assert.NotNull(await rig.HistoryBlocks.GetByNumberAsync(1));
                Assert.Equal(2, (await rig.HistoryTransactions.GetByBlockNumberAsync(1)).Count);
                Assert.Equal(2, (await rig.HistoryReceipts.GetByBlockNumberAsync(1)).Count);
            }
        }

        private sealed class TestBlock
        {
            public BlockHeader Header;
            public byte[] Hash;
            public BigInteger Number;
            public List<ISignedTransaction> Transactions;
            public IReadOnlyList<ReceiptSaveItem> Receipts;
        }

        private static TestBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var header = MakeHeader(number, hash);
            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };
            var receipts = new List<ReceiptSaveItem>
            {
                new ReceiptSaveItem(
                    new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() },
                    txs[0].Hash, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
                new ReceiptSaveItem(
                    new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 42000, Logs = new List<Log>() },
                    txs[1].Hash, txIndex: 1, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
            };
            return new TestBlock
            {
                Header = header,
                Hash = hash,
                Number = header.BlockNumber.ToBigInteger(),
                Transactions = txs,
                Receipts = receipts,
            };
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            var bloom = new byte[256];
            bloom[0] = 0xAB;
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = Fill32(number - 1),
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1000 + number,
                ExtraData = Array.Empty<byte>(),
                MixHash = hash,
                Nonce = new byte[8],
                LogsBloom = bloom,
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static ISignedTransaction MakeTx(long number, int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill32(long number)
        {
            var b = new byte[32];
            b[24] = 0x77;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
