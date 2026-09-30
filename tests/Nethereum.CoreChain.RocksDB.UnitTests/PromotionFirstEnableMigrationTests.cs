using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionFirstEnableMigrationTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionfirstenable_{Guid.NewGuid():N}");

        public PromotionFirstEnableMigrationTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static RocksDbPromotionService PromotionServiceOf(RocksDbChainStoreBundle bundle)
            => (RocksDbPromotionService)typeof(RocksDbChainStoreBundle)
                .GetField("_promotionService", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(bundle);

        private static RocksDbHotBlockWindowStore HotWindowOf(RocksDbChainStoreBundle bundle)
            => (RocksDbHotBlockWindowStore)typeof(RocksDbChainStoreBundle)
                .GetField("_hotWindow", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(bundle);

        private static async Task<TestBlock> WriteToHistoryAsync(RocksDbChainStoreBundle bundle, long number)
        {
            var block = MakeBlock(number);
            await bundle.Blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Number, block.Transactions).ConfigureAwait(false);
            await bundle.Receipts.SaveManyAsync(block.Hash, block.Number, block.Receipts).ConfigureAwait(false);
            return block;
        }

        [Fact]
        public async Task FirstEnable_OnPopulatedDb_InitializesCursorToHead_NoFailLoud()
        {
            var dir = SubDir("populated");
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)3 };

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = false }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                TestBlock last = null;
                for (long n = 1; n <= 5; n++)
                    last = await WriteToHistoryAsync(bundle, n);
                bundle.Metadata.CommitDurableState((ulong)last.Number, last.Hash);
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

                var ex = await Record.ExceptionAsync(() => bundle.EnsureConsistentHeadAsync(_ => { }));
                Assert.Null(ex);

                Assert.Equal(5UL, ((RocksDbChainMetadataStore)bundle.Metadata).GetPromotionCursor());

                var hotWindow = HotWindowOf(bundle);
                for (long n = 1; n <= 5; n++)
                    Assert.False(hotWindow.ContainsBlock((ulong)n), $"block {n} must not have been promoted from an empty hot window");

                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(3));
                Assert.Equal(2, (await bundle.Transactions.GetByBlockNumberAsync(3)).Count);
                Assert.Equal(2, (await bundle.Receipts.GetByBlockNumberAsync(3)).Count);
            }
        }

        [Fact]
        public async Task FirstEnable_ThenFollow_PromotesOnlyNewBlocks()
        {
            var dir = SubDir("then-follow");
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)3 };

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = false }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                TestBlock last = null;
                for (long n = 1; n <= 5; n++)
                    last = await WriteToHistoryAsync(bundle, n);
                bundle.Metadata.CommitDurableState((ulong)last.Number, last.Hash);
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                await bundle.EnsureConsistentHeadAsync(_ => { });
                var metadata = (RocksDbChainMetadataStore)bundle.Metadata;
                Assert.Equal(5UL, metadata.GetPromotionCursor());

                var promotion = PromotionServiceOf(bundle);
                var hotWindow = HotWindowOf(bundle);

                for (long n = 6; n <= 10; n++)
                {
                    var newBlock = MakeBlock(n);
                    await bundle.Blocks.SaveAsync(newBlock.Header, newBlock.Hash).ConfigureAwait(false);
                    await bundle.Transactions.SaveManyAsync(newBlock.Hash, newBlock.Number, newBlock.Transactions).ConfigureAwait(false);
                    await bundle.Receipts.SaveManyAsync(newBlock.Hash, newBlock.Number, newBlock.Receipts).ConfigureAwait(false);

                    metadata.CommitDurableState((ulong)n, newBlock.Hash);
                    promotion.PromoteDurableBlocks((ulong)n);
                }

                Assert.Equal(7UL, metadata.GetPromotionCursor());
                Assert.False(hotWindow.ContainsBlock(6UL));
                Assert.False(hotWindow.ContainsBlock(7UL));
                Assert.True(hotWindow.ContainsBlock(8UL));
                Assert.True(hotWindow.ContainsBlock(9UL));
                Assert.True(hotWindow.ContainsBlock(10UL));

                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(6));
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(1));
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(5));
            }
        }

        [Fact]
        public async Task FirstEnable_DurableHeadBehindLastStoredBlock_AnchorsCursorAtDurableHead_NoFailLoud()
        {
            var dir = SubDir("torn-behind");
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)3 };

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = false }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                for (long n = 1; n <= 5; n++)
                    await WriteToHistoryAsync(bundle, n);
                bundle.Metadata.CommitDurableState(2UL, Fill32(2));
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

                var ex = await Record.ExceptionAsync(() => bundle.EnsureConsistentHeadAsync(_ => { }));
                Assert.Null(ex);

                Assert.Equal(2UL, ((RocksDbChainMetadataStore)bundle.Metadata).GetPromotionCursor());

                var hotWindow = HotWindowOf(bundle);
                for (long n = 1; n <= 5; n++)
                    Assert.False(hotWindow.ContainsBlock((ulong)n), $"block {n} must not have been chased into an empty hot window");

                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(1));
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(3));
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(5));
                Assert.Equal(2, (await bundle.Transactions.GetByBlockNumberAsync(5)).Count);
                Assert.Equal(2, (await bundle.Receipts.GetByBlockNumberAsync(5)).Count);
            }
        }

        [Fact]
        public async Task FirstEnable_DurableHeadAheadOfLastStoredBlock_AnchorsCursorAtDurableHead_NoFailLoud()
        {
            var dir = SubDir("torn-ahead");
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)3 };

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = false }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                for (long n = 1; n <= 3; n++)
                    await WriteToHistoryAsync(bundle, n);
                bundle.Metadata.CommitDurableState(10UL, Fill32(10));
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

                var ex = await Record.ExceptionAsync(() => bundle.EnsureConsistentHeadAsync(_ => { }));
                Assert.Null(ex);

                Assert.Equal(10UL, ((RocksDbChainMetadataStore)bundle.Metadata).GetPromotionCursor());

                var hotWindow = HotWindowOf(bundle);
                for (long n = 1; n <= 10; n++)
                    Assert.False(hotWindow.ContainsBlock((ulong)n), $"block {n} must not have been chased into an empty hot window");

                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(1));
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(3));
                for (long n = 4; n <= 10; n++)
                    Assert.Null(await bundle.Blocks.GetByNumberAsync(n));
            }
        }

        [Fact]
        public async Task SecondBoot_CursorAlreadySet_DoesNotReinitialize()
        {
            var dir = SubDir("second-boot");
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)3 };

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = false }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                TestBlock last = null;
                for (long n = 1; n <= 5; n++)
                    last = await WriteToHistoryAsync(bundle, n);
                bundle.Metadata.CommitDurableState((ulong)last.Number, last.Hash);
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                await bundle.EnsureConsistentHeadAsync(_ => { });
                Assert.Equal(5UL, ((RocksDbChainMetadataStore)bundle.Metadata).GetPromotionCursor());

                var newBlock = MakeBlock(6);
                await bundle.Blocks.SaveAsync(newBlock.Header, newBlock.Hash).ConfigureAwait(false);
                await bundle.Transactions.SaveManyAsync(newBlock.Hash, newBlock.Number, newBlock.Transactions).ConfigureAwait(false);
                bundle.Metadata.CommitDurableState((ulong)newBlock.Number, newBlock.Hash);
            }

            using (var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true }))
            {
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
                await bundle.EnsureConsistentHeadAsync(_ => { });

                Assert.Equal(5UL, ((RocksDbChainMetadataStore)bundle.Metadata).GetPromotionCursor());
                Assert.True(HotWindowOf(bundle).ContainsBlock(6UL), "second boot must not disturb an already-set cursor or evict a hot block below its floor");
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
            b[24] = 0x99;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
