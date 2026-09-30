using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HotBlockWindowSourceAdapterTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"hotwindowadapter_{Guid.NewGuid():N}");

        public HotBlockWindowSourceAdapterTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_AHotBlock_When_ReadThroughTheAdapter_Then_ItsReceiptsUnclesWithdrawalsMapToHotBlockShape()
        {
            var dir = Path.Combine(_root, "shape");
            using var mgr = NewCoreManager(dir);
            var window = NewWindow(mgr);

            var hash = Fill32(500);
            var uncleHash = Fill32(499);
            var uncle = MakeHeader(499, Fill32(498));
            var txs = new List<ISignedTransaction> { MakeTx(0), MakeTx(1) };
            var withdrawal = new Withdrawal { Index = 1, ValidatorIndex = 7, Address = Fill20(9), AmountInGwei = 42 };
            var balRlp = new byte[] { 0x01, 0x02, 0x03 };
            var receiptItems = new List<ReceiptSaveItem>
            {
                new ReceiptSaveItem(new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() },
                    txs[0].Hash, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
                new ReceiptSaveItem(new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 42000, Logs = new List<Log>() },
                    txs[1].Hash, txIndex: 1, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
            };

            await WriteFullBlockAsync(mgr, window, 500, hash, parentHash: Fill32(499),
                txs: txs, uncles: new List<BlockHeader> { uncle }, withdrawals: new List<Withdrawal> { withdrawal },
                receiptItems: receiptItems, balRlp: balRlp);

            var adapter = new RocksDbHotBlockWindowSourceAdapter(mgr, window);
            var hotBlock = adapter.ReadHotBlock(500);

            Assert.Equal(500, hotBlock.Header.BlockNumber.ToLong());
            Assert.Equal(hash, hotBlock.BlockHash);

            Assert.Equal(2, hotBlock.Body.Txs.Count);
            Assert.Equal(txs[0].Hash, hotBlock.Body.Txs[0].Hash);
            Assert.Equal(txs[1].Hash, hotBlock.Body.Txs[1].Hash);

            Assert.Single(hotBlock.Body.Uncles);
            Assert.Equal(uncle.BlockNumber.ToLong(), hotBlock.Body.Uncles[0].BlockNumber.ToLong());
            Assert.Equal(uncle.ParentHash, hotBlock.Body.Uncles[0].ParentHash);

            Assert.NotNull(hotBlock.Body.Withdrawals);
            Assert.Single(hotBlock.Body.Withdrawals);
            Assert.Equal(withdrawal.Index, hotBlock.Body.Withdrawals[0].Index);
            Assert.Equal(withdrawal.ValidatorIndex, hotBlock.Body.Withdrawals[0].ValidatorIndex);
            Assert.Equal(withdrawal.Address, hotBlock.Body.Withdrawals[0].Address);
            Assert.Equal(withdrawal.AmountInGwei, hotBlock.Body.Withdrawals[0].AmountInGwei);

            Assert.Equal(2, hotBlock.Receipts.Count);
            Assert.Equal(21000, (long)hotBlock.Receipts[0].CumulativeGasUsed);
            Assert.Equal(42000, (long)hotBlock.Receipts[1].CumulativeGasUsed);

            Assert.Equal(balRlp, hotBlock.BalRlp);
        }

        [Fact]
        public async Task Given_APreShanghaiBlock_When_MappedThroughTheAdapter_Then_WithdrawalsStayNull_NotEmptyList()
        {
            var dir = Path.Combine(_root, "preshanghai");
            using var mgr = NewCoreManager(dir);
            var window = NewWindow(mgr);

            var hash = Fill32(600);
            var txs = new List<ISignedTransaction> { MakeTx(0) };

            await WriteFullBlockAsync(mgr, window, 600, hash, parentHash: Fill32(599),
                txs: txs, uncles: new List<BlockHeader>(), withdrawals: null,
                receiptItems: new List<ReceiptSaveItem>(), balRlp: Array.Empty<byte>());

            var adapter = new RocksDbHotBlockWindowSourceAdapter(mgr, window);
            var hotBlock = adapter.ReadHotBlock(600);

            Assert.Null(hotBlock.Body.Withdrawals);
        }

        [Fact]
        public void Given_AnEmptyHotWindow_When_HotTipNumber_Then_ItReturnsTheDefinedSentinel_AndPromotionUpperBoundClampsToIt()
        {
            var dir = Path.Combine(_root, "emptywindow");
            using var mgr = NewCoreManager(dir);
            var window = NewWindow(mgr);
            var adapter = new RocksDbHotBlockWindowSourceAdapter(mgr, window);

            Assert.Equal(RocksDbHotBlockWindowSourceAdapter.EmptyWindowSentinel, adapter.HotTipNumber);

            var freezerDir = Path.Combine(_root, "emptywindow-freezer");
            Directory.CreateDirectory(freezerDir);
            using var freezer = FreezerCore.Open(new FreezerLayout(freezerDir), FreezerOpenMode.Append);
            var codecs = new FreezerCodecSet();
            var finality = new StubFinalitySource { FinalizedBlockNumber = 100 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(freezer, codecs, adapter, finality, indexes);

            var result = service.PromoteFinalizedBlocks();

            Assert.Equal(0, result.PromotedCount);
            Assert.Equal(0, result.NewFreezerItems);
            Assert.Equal(0, freezer.Items);
        }

        [Fact]
        public void Given_PromotedBlocks_When_EvictAtOrBelow_Then_HotNoLongerServesThem()
        {
            var dir = Path.Combine(_root, "evict");
            using var mgr = NewCoreManager(dir);
            var window = NewWindow(mgr);
            var adapter = new RocksDbHotBlockWindowSourceAdapter(mgr, window);

            for (long n = 1; n <= 5; n++)
                window.WriteHeader(MakeHeader(n, Fill32(n - 1)), Fill32(n));

            adapter.EvictAtOrBelow(3);

            for (long n = 1; n <= 3; n++)
            {
                Assert.False(window.ContainsBlock((ulong)n));
                Assert.Null(window.ReadForPromotion((ulong)n));
            }
        }

        [Fact]
        public void Given_UnpromotedBlockAboveBound_When_Evict_Then_ItRemainsHot()
        {
            var dir = Path.Combine(_root, "evict-twin");
            using var mgr = NewCoreManager(dir);
            var window = NewWindow(mgr);
            var adapter = new RocksDbHotBlockWindowSourceAdapter(mgr, window);

            for (long n = 1; n <= 5; n++)
                window.WriteHeader(MakeHeader(n, Fill32(n - 1)), Fill32(n));

            adapter.EvictAtOrBelow(3);

            for (long n = 4; n <= 5; n++)
            {
                Assert.True(window.ContainsBlock((ulong)n));
                Assert.NotNull(window.ReadForPromotion((ulong)n));
                Assert.Equal(n, adapter.ReadHotBlock(n).Header.BlockNumber.ToLong());
            }
        }

        private sealed class StubFinalitySource : IFinalitySource
        {
            public long FinalizedBlockNumber { get; set; }
        }

        private static RocksDbManager NewCoreManager(string dir) =>
            new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }, CatalogueScope.Core);

        private static RocksDbHotBlockWindowStore NewWindow(RocksDbManager mgr) =>
            new RocksDbHotBlockWindowStore(mgr, windowSize: 1000, evictOnWrite: false);

        private static async Task WriteFullBlockAsync(
            RocksDbManager mgr, RocksDbHotBlockWindowStore window, long number, byte[] hash, byte[] parentHash,
            List<ISignedTransaction> txs, List<BlockHeader> uncles, List<Withdrawal> withdrawals,
            List<ReceiptSaveItem> receiptItems, byte[] balRlp)
        {
            window.WriteHeader(MakeHeader(number, parentHash), hash);
            if (txs.Count > 0) window.WriteTransactions((ulong)number, txs);

            var uncleStore = new RocksDbUncleStore(mgr, blockStore: null,
                blockMetaCf: RocksDbManager.CF_HOT_BLOCK_META, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
            await uncleStore.SaveAsync(hash, uncles);

            if (withdrawals != null)
            {
                var withdrawalStore = new RocksDbWithdrawalStore(mgr, blockStore: null,
                    blockMetaCf: RocksDbManager.CF_HOT_BLOCK_META, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
                await withdrawalStore.SaveAsync(hash, withdrawals);
            }

            if (receiptItems.Count > 0)
            {
                var receiptStore = new RocksDbReceiptStore(mgr, blockStore: null,
                    receiptBodyCf: RocksDbManager.CF_HOT_RECEIPT_BODY, txHashIndexCf: RocksDbManager.CF_HOT_TX_HASH_INDEX, writeTxHashIndex: false);
                await receiptStore.SaveManyAsync(hash, number, receiptItems);
            }

            if (balRlp != null && balRlp.Length > 0)
            {
                var balStore = new RocksDbBlockAccessListStore(mgr,
                    blockAccessListCf: RocksDbManager.CF_HOT_BLOCK_ACCESS_LIST, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
                await balStore.SaveAsync(hash, balRlp);
            }
        }

        private static BlockHeader MakeHeader(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = parentHash,
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1000,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private static ISignedTransaction MakeTx(int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(index + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: Array.Empty<byte>(),
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill32(long number)
        {
            var b = new byte[32];
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }

        private static byte[] Fill20(int seed)
        {
            var b = new byte[20];
            b[19] = (byte)seed;
            return b;
        }
    }
}
