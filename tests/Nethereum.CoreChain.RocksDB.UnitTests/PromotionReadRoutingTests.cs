using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionReadRoutingTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionreads_{Guid.NewGuid():N}");
        private static readonly string LogAddress = "0x" + new string('7', 40);

        public PromotionReadRoutingTests() => Directory.CreateDirectory(_root);

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

        private static RocksDbChainStoreBundle OpenPromotionBundle(RocksDbManager manager, string dir, ulong maxHistoryBlocks)
        {
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)maxHistoryBlocks };
            return RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);
        }

        private static RocksDbPromotionService PromotionServiceOf(RocksDbChainStoreBundle bundle)
            => bundle.PromotionService;

        private static RocksDbHotBlockWindowStore HotWindowOf(RocksDbChainStoreBundle bundle)
            => bundle.HotWindow;

        [Fact]
        public async Task RpcRead_PromotedBlock_IdenticalToHotBlock()
        {
            var dir = SubDir("prom07");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);

            for (long n = 1; n <= 5; n++)
                await WriteBlockAsync(bundle, MakeBlock(n));

            var target = (BigInteger)2;
            var before = await ReadAllAsync(bundle, target);
            Assert.NotNull(before.Header);
            Assert.True(HotWindowOf(bundle).ContainsBlock(2), "precondition: block 2 is still hot before promotion");

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            Assert.False(HotWindowOf(bundle).ContainsBlock(2), "block 2 must have moved out of hot after promotion");
            var after = await ReadAllAsync(bundle, target);
            AssertSnapshotsEqual(before, after);
        }

        [Fact]
        public async Task GetLogs_RangeSpanningPromotionCursor_NoDropNoDouble()
        {
            var dir = SubDir("getlogs-range");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);

            for (long n = 1; n <= 5; n++)
                await WriteBlockAsync(bundle, MakeBlock(n));

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            var logs = await bundle.Logs.GetLogsAsync(new LogFilter { FromBlock = 1, ToBlock = 5 });

            var byBlock = logs.Select(l => l.BlockNumber).OrderBy(n => n).ToList();
            Assert.Equal(new List<BigInteger> { 1, 2, 3, 4, 5 }, byBlock);
        }

        [Fact]
        public async Task GetLogsByBlockHashAndByTxHash_PromotedAndHot_BothFound()
        {
            var dir = SubDir("getlogs-byhash");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);

            var blocks = new List<TestBlock>();
            for (long n = 1; n <= 5; n++)
            {
                var block = MakeBlock(n);
                await WriteBlockAsync(bundle, block);
                blocks.Add(block);
            }

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            var promoted = blocks[0];
            var hot = blocks[4];

            Assert.NotEmpty(await bundle.Logs.GetLogsByBlockHashAsync(promoted.Hash));
            Assert.NotEmpty(await bundle.Logs.GetLogsByBlockHashAsync(hot.Hash));
            Assert.NotEmpty(await bundle.Logs.GetLogsByTxHashAsync(promoted.Transactions[0].Hash));
            Assert.NotEmpty(await bundle.Logs.GetLogsByTxHashAsync(hot.Transactions[0].Hash));
        }

        [Fact]
        public async Task WithoutHistoryFallback_PromotedReceiptRead_ReturnsEmpty_ProvesCompositeNecessary()
        {
            var dir = SubDir("red-composite");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);

            for (long n = 1; n <= 5; n++)
                await WriteBlockAsync(bundle, MakeBlock(n));

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            var hotOnly = new RocksDbReceiptStore(manager, bundle.Blocks,
                receiptBodyCf: RocksDbManager.CF_HOT_RECEIPT_BODY, txHashIndexCf: RocksDbManager.CF_HOT_TX_HASH_INDEX,
                writeTxHashIndex: false);

            Assert.Empty(await hotOnly.GetByBlockNumberAsync(2));
            Assert.NotEmpty(await bundle.Receipts.GetByBlockNumberAsync(2));
        }

        [Fact]
        public async Task GetLogs_WithoutHotUnion_MissesUnpromotedBlocks_ProvesUnionNecessary()
        {
            var dir = SubDir("red-getlogs");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);

            for (long n = 1; n <= 5; n++)
                await WriteBlockAsync(bundle, MakeBlock(n));

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            var historyOnly = new HistoryBloomScanLogStore(manager);
            var missing = await historyOnly.GetLogsAsync(new LogFilter { FromBlock = 1, ToBlock = 5 });
            Assert.Equal(2, missing.Count);

            var union = await bundle.Logs.GetLogsAsync(new LogFilter { FromBlock = 1, ToBlock = 5 });
            Assert.Equal(5, union.Count);
        }

        private static async Task WriteBlockAsync(RocksDbChainStoreBundle bundle, TestBlock block)
        {
            await bundle.Blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Number, block.Transactions).ConfigureAwait(false);
            await bundle.Uncles.SaveAsync(block.Hash, block.Uncles).ConfigureAwait(false);
            await bundle.Withdrawals.SaveAsync(block.Hash, block.Withdrawals).ConfigureAwait(false);
            await bundle.Receipts.SaveManyAsync(block.Hash, block.Number, block.Receipts).ConfigureAwait(false);
        }

        private static async Task<BlockSnapshot> ReadAllAsync(RocksDbChainStoreBundle bundle, BigInteger number)
        {
            return new BlockSnapshot(
                await bundle.Blocks.GetByNumberAsync(number).ConfigureAwait(false),
                await bundle.Transactions.GetByBlockNumberAsync(number).ConfigureAwait(false),
                await bundle.Receipts.GetByBlockNumberAsync(number).ConfigureAwait(false),
                await bundle.Uncles.GetByBlockNumberAsync(number).ConfigureAwait(false),
                await bundle.Withdrawals.GetByBlockNumberAsync(number).ConfigureAwait(false),
                await bundle.Logs.GetLogsByBlockNumberAsync(number).ConfigureAwait(false));
        }

        private static void AssertSnapshotsEqual(BlockSnapshot before, BlockSnapshot after)
        {
            Assert.NotNull(before.Header);
            Assert.NotNull(after.Header);
            Assert.Equal(Hex(RlpBlockEncodingProvider.Instance.EncodeBlockHeader(before.Header)),
                Hex(RlpBlockEncodingProvider.Instance.EncodeBlockHeader(after.Header)));

            Assert.Equal(before.Transactions.Count, after.Transactions.Count);
            for (int i = 0; i < before.Transactions.Count; i++)
                Assert.Equal(Hex(before.Transactions[i].Hash), Hex(after.Transactions[i].Hash));

            Assert.Equal(before.Receipts.Count, after.Receipts.Count);
            for (int i = 0; i < before.Receipts.Count; i++)
            {
                Assert.Equal(before.Receipts[i].CumulativeGasUsed, after.Receipts[i].CumulativeGasUsed);
                Assert.Equal(before.Receipts[i].Logs.Count, after.Receipts[i].Logs.Count);
            }

            Assert.Equal(before.Uncles.Count, after.Uncles.Count);
            for (int i = 0; i < before.Uncles.Count; i++)
                Assert.Equal(Hex(RlpBlockEncodingProvider.Instance.EncodeBlockHeader(before.Uncles[i])),
                    Hex(RlpBlockEncodingProvider.Instance.EncodeBlockHeader(after.Uncles[i])));

            Assert.Equal(before.Withdrawals.Count, after.Withdrawals.Count);
            for (int i = 0; i < before.Withdrawals.Count; i++)
            {
                Assert.Equal(before.Withdrawals[i].Index, after.Withdrawals[i].Index);
                Assert.Equal(before.Withdrawals[i].ValidatorIndex, after.Withdrawals[i].ValidatorIndex);
                Assert.Equal(before.Withdrawals[i].AmountInGwei, after.Withdrawals[i].AmountInGwei);
            }

            Assert.Equal(before.Logs.Count, after.Logs.Count);
            for (int i = 0; i < before.Logs.Count; i++)
                Assert.Equal(Hex(RocksDbSerializer.SerializeFilteredLog(before.Logs[i])),
                    Hex(RocksDbSerializer.SerializeFilteredLog(after.Logs[i])));
        }

        private static string Hex(byte[] bytes) => Convert.ToHexString(bytes ?? Array.Empty<byte>());

        private sealed class BlockSnapshot
        {
            public readonly BlockHeader Header;
            public readonly List<ISignedTransaction> Transactions;
            public readonly List<Receipt> Receipts;
            public readonly IList<BlockHeader> Uncles;
            public readonly IList<Withdrawal> Withdrawals;
            public readonly List<FilteredLog> Logs;

            public BlockSnapshot(BlockHeader header, List<ISignedTransaction> transactions, List<Receipt> receipts,
                IList<BlockHeader> uncles, IList<Withdrawal> withdrawals, List<FilteredLog> logs)
            {
                Header = header;
                Transactions = transactions;
                Receipts = receipts;
                Uncles = uncles;
                Withdrawals = withdrawals;
                Logs = logs;
            }
        }

        private sealed class TestBlock
        {
            public BlockHeader Header;
            public byte[] Hash;
            public BigInteger Number;
            public List<ISignedTransaction> Transactions;
            public IList<BlockHeader> Uncles;
            public IList<Withdrawal> Withdrawals;
            public IReadOnlyList<ReceiptSaveItem> Receipts;
        }

        private static TestBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var header = MakeHeader(number, hash);
            var tx = MakeTx(number);
            var uncle = MakeHeader(1000 + number, Fill32(1000 + number));
            var withdrawal = new Withdrawal
            {
                Index = (ulong)number, ValidatorIndex = (ulong)(100 + number), Address = new byte[20], AmountInGwei = (ulong)(number * 10),
            };
            var log = Log.Create(new byte[] { (byte)number }, LogAddress, Fill32(2000 + number));
            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 * number, Logs = new List<Log> { log } };

            return new TestBlock
            {
                Header = header,
                Hash = hash,
                Number = header.BlockNumber.ToBigInteger(),
                Transactions = new List<ISignedTransaction> { tx },
                Uncles = new List<BlockHeader> { uncle },
                Withdrawals = new List<Withdrawal> { withdrawal },
                Receipts = new List<ReceiptSaveItem>
                {
                    new ReceiptSaveItem(receipt, tx.Hash, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
                },
            };
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
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
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static ISignedTransaction MakeTx(long number)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1),
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
            b[24] = 0x66;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
