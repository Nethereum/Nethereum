using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerFollowPromotionTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerfollow_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long ImmutabilityThreshold = 90_000;

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private RocksDbStorageOptions FollowOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
            PromotionEnabled = true,
        };

        private RocksDbChainStoreBundle OpenBundle()
            => RocksDbChainStoreBundle.Open(DataDir, null, false, FollowOptions(), _signer);

        private static readonly Nethereum.Freezer.FilterMaps.FilterMapsParams TinyParams =
            new Nethereum.Freezer.FilterMaps.FilterMapsParams(
                logMapHeight: 4, logMapWidth: 8, logMapsPerEpoch: 1, logValuesPerMap: 3,
                baseRowGroupSize: 2, baseRowLengthRatio: 4, logLayerDiff: 4);

        private RocksDbChainStoreBundle OpenBundleWithLogIndexParams()
        {
            var options = FollowOptions();
            options.FilterMapsIndexParams = TinyParams;
            return RocksDbChainStoreBundle.Open(DataDir, null, false, options, _signer);
        }

        private const long TinyFreezerFileSizeBytes = 8;

        private RocksDbChainStoreBundle OpenBundleWithBackgroundIndexing()
        {
            var options = FollowOptions();
            options.BackgroundFreezeIndexing = true;
            options.FreezerMaxFileSizeBytes = TinyFreezerFileSizeBytes;
            return RocksDbChainStoreBundle.Open(DataDir, null, false, options, _signer);
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.True(condition(), "the trailer did not converge within the timeout");
        }

        private static object GetPrivateField(RocksDbChainStoreBundle bundle, string name)
            => typeof(RocksDbChainStoreBundle).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bundle);

        private static void SetIndexTrailerTaskForTests(RocksDbChainStoreBundle bundle, Task value)
        {
            var indexer = GetPrivateField(bundle, "_freezerIndexer");
            typeof(Nethereum.CoreChain.RocksDB.Freezer.FreezerBackgroundIndexer)
                .GetField("_byHashTask", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(indexer, value);
        }

        private static RocksDbPromotionService RocksPromotionServiceOf(RocksDbChainStoreBundle bundle)
            => (RocksDbPromotionService)GetPrivateField(bundle, "_promotionService");

        private static object FreezerPromotionServiceOf(RocksDbChainStoreBundle bundle)
        {
            var driver = GetPrivateField(bundle, "_freezerPromotionDriver");
            if (driver == null) return null;
            return driver.GetType().GetField("_freezerPromotionService", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
        }

        private static RocksDbHotBlockWindowStore HotWindowOf(RocksDbChainStoreBundle bundle)
            => (RocksDbHotBlockWindowStore)GetPrivateField(bundle, "_hotWindow");

        private static void DriveFreezerPromotion(RocksDbChainStoreBundle bundle)
        {
            var driver = GetPrivateField(bundle, "_freezerPromotionDriver");
            driver.GetType().GetMethod("Drive", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, null);
        }

        private static void AppendToFreezerWithoutIndexing(RocksDbChainStoreBundle bundle, List<PersistableBlock> blocks)
        {
            var appendService = GetPrivateField(bundle, "_freezerAppendService");
            appendService.GetType()
                .GetMethod("AppendToFreezer", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(appendService, new object[] { blocks, blocks.Count });
        }

        private static void StampTipHeight(RocksDbManager rocks, long height)
            => rocks.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.UTF8.GetBytes("height"),
                RocksDbSerializer.BigIntegerToBytes(height));

        private static RocksDbManager CoreManagerOf(RocksDbChainStoreBundle bundle)
            => (RocksDbManager)typeof(RocksDbChainStoreBundle).GetField("_rocks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bundle);

        private static BigInteger ReadTipHeight(RocksDbManager rocks)
        {
            var raw = rocks.Get(RocksDbManager.CF_METADATA, System.Text.Encoding.UTF8.GetBytes("height"));
            return raw == null ? BigInteger.MinusOne : RocksDbSerializer.BytesToBigInteger(raw);
        }

        [Fact]
        public async Task Given_AFollowedBlockAgesPastTheBound_When_Flush_Then_ItIsFrozen_Indexed_AndEvictedFromHot()
        {
            using var bundle = OpenBundle();
            var blocks = MakeChainedBlocks(0, 6);
            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);

            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 3);

            Assert.Equal((ulong?)5, HotWindowOf(bundle).TryGetLatestNumber());

            DriveFreezerPromotion(bundle);

            Assert.Equal(4, bundle.FreezerHead);
            Assert.Equal(4, bundle.ByHashIndexedHead);

            var promoted = blocks[3];
            var header = await bundle.Blocks.GetByHashAsync(promoted.Hash);
            Assert.NotNull(header);
            Assert.Equal(promoted.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());

            Assert.False(HotWindowOf(bundle).ContainsBlock(3), "block 3 must have been evicted from hot once frozen");
            Assert.True(HotWindowOf(bundle).ContainsBlock(4), "block 4 is above the bound and must stay hot");
        }

        [Fact]
        public async Task Given_ALiveFollowedBlock_When_Saved_Then_TheTipStampAdvances_AndPromotionFollows()
        {
            using var bundle = OpenBundle();
            var blocks = MakeChainedBlocks(0, 6);
            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);

            var tipHeader = MakeHeader(ImmutabilityThreshold + 3, Fill32(999));
            await bundle.Blocks.SaveAsync(tipHeader, Fill32(999));

            Assert.Equal((BigInteger)(ImmutabilityThreshold + 3), ReadTipHeight(CoreManagerOf(bundle)));

            DriveFreezerPromotion(bundle);

            Assert.Equal(4, bundle.FreezerHead);
            Assert.False(HotWindowOf(bundle).ContainsBlock(3), "block 3 must have been evicted from hot once frozen");
            Assert.True(HotWindowOf(bundle).ContainsBlock(4), "block 4 is above the bound and must stay hot");
        }

        [Fact]
        public void Given_NoBlockPastBound_When_Flush_Then_NoPromotion()
        {
            using var bundle = OpenBundle();

            StampTipHeight(CoreManagerOf(bundle), 5);

            DriveFreezerPromotion(bundle);

            Assert.Equal(0, bundle.FreezerHead);
        }

        [Fact]
        public async Task Given_UseFreezerHistoryAndPromotion_When_Constructed_Then_OnlyFreezerPromotionRuns_NotRocksDbPromotion()
        {
            using var bundle = OpenBundle();

            Assert.Null(RocksPromotionServiceOf(bundle));
            Assert.NotNull(FreezerPromotionServiceOf(bundle));

            var blocks = MakeChainedBlocks(0, 4);
            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);
            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 1);

            DriveFreezerPromotion(bundle);

            Assert.Equal(2, bundle.FreezerHead);

            var core = CoreManagerOf(bundle);
            var rawHeader = core.Get(HistoryColumnFamilies.BlockHeader, HistoryKeys.BlockKey(0));
            Assert.Null(rawHeader);
        }

        [Fact]
        public async Task Given_BulkBackfillAndFollowPromotionConcurrent_When_BothAppend_Then_FreezerItemsStayContiguous_NoRace()
        {
            using var bundle = OpenBundle();
            var blocks = MakeChainedBlocks(0, 10);

            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);
            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 9);

            var bulkTask = Task.Run(() => bundle.PersistBlocksAsync(blocks));
            var followTask = Task.Run(() => DriveFreezerPromotion(bundle));

            await Task.WhenAll(bulkTask, followTask);

            Assert.Equal(10, bundle.FreezerHead);
            for (var i = 0; i < 10; i++)
            {
                var header = await bundle.Blocks.GetByNumberAsync(i);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Given_CrashBetweenFreezerCommitAndIndex_When_Reboot_Then_TheLaggingIndexIsRebuilt_AndByHashResolves()
        {
            var blocks = MakeChainedBlocks(0, 2);

            using (var bundle = OpenBundle())
            {
                AppendToFreezerWithoutIndexing(bundle, blocks);

                Assert.Equal(2, bundle.FreezerHead);
                Assert.Null(await bundle.Blocks.GetByHashAsync(blocks[0].Hash));
            }

            using (var reopened = OpenBundle())
            {
                reopened.FinishBulkIndexing();

                Assert.Equal(2, reopened.FreezerHead);
                Assert.Equal(2, reopened.ByHashIndexedHead);

                var header = await reopened.Blocks.GetByHashAsync(blocks[0].Hash);
                Assert.NotNull(header);
                Assert.Equal(0, (int)header.BlockNumber.ToBigInteger());
            }
        }

        // Task 6 (freezer-as-history-backend plan): the gap this task closes -- IndexPromoted (by-hash only)
        // left the EIP-7745 log index behind, so a follow-promoted block's logs were unreachable from the
        // rendered filtermaps index (only from the O(n) hot-scan fallback over the whole range). Composes
        // RenderFilterMapsCatchUp into DriveFreezerPromotion; asserts the index itself advanced
        // (LogIndexRenderedHead), then confirms eth_getLogs resolves EXCLUSIVELY through that rendered range
        // (ToBlock capped at the rendered head, never spilling into the hot-scan fallback that would mask a
        // missing render).
        [Fact]
        public async Task Given_AFollowedBlockWithLogs_When_Promoted_Then_GetLogsResolvesFromHistory()
        {
            using var bundle = OpenBundleWithLogIndexParams();
            var blocks = MakeChainedBlocksWithLogs(0, 11);
            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);

            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 10);

            DriveFreezerPromotion(bundle);

            Assert.Equal(11, bundle.FreezerHead);
            Assert.True(bundle.LogIndexRenderedHead > 0,
                "the log index must have rendered at least one epoch across the promoted range");

            var indexedHead = bundle.LogIndexRenderedHead - 1;
            var filter = new LogFilter
            {
                Addresses = new List<string> { LogAddress },
                FromBlock = 0,
                ToBlock = indexedHead,
            };
            var results = await bundle.Logs.GetLogsAsync(filter);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Given_PromotionEnabledWithTrailerRunningAndNoPhase1Backfill_When_OlderBacklogExistsAndPromotionAppends_Then_PromotionNeverAdvancesTheCursorItselfAndTheBacklogIsNotStranded()
        {
            using var bundle = OpenBundleWithBackgroundIndexing();
            var blocks = MakeChainedBlocks(0, 6);

            AppendToFreezerWithoutIndexing(bundle, blocks.GetRange(0, 2));
            Assert.Equal(2, bundle.FreezerHead);
            Assert.Equal(0, bundle.ByHashIndexedHead);

            SetIndexTrailerTaskForTests(bundle, Task.CompletedTask);

            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);
            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 5);

            DriveFreezerPromotion(bundle);
            Assert.Equal(6, bundle.FreezerHead);

            Assert.Equal(0, bundle.ByHashIndexedHead);

            bundle.FinishBulkIndexing();
            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);

            var backlog = blocks[0];
            var resolved = await bundle.Blocks.GetByHashAsync(backlog.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(0, (int)resolved.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_PromotionEnabledWithTrailerRunning_When_ABlockIsPromoted_Then_TheTrailerIndexesItFromTheFrozenFiles()
        {
            using var bundle = OpenBundleWithBackgroundIndexing();
            bundle.StartBackgroundFreezeIndexing();

            var blocks = MakeChainedBlocks(0, 6);
            foreach (var b in blocks)
                await WriteToHotAsync(bundle, b);
            StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 5);

            DriveFreezerPromotion(bundle);
            Assert.Equal(6, bundle.FreezerHead);

            await WaitUntilAsync(() => bundle.ByHashIndexedHead >= bundle.FreezerHead - 1, TimeSpan.FromSeconds(15));

            var promoted = blocks[3];
            var resolved = await bundle.Blocks.GetByHashAsync(promoted.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(3, (int)resolved.BlockNumber.ToBigInteger());
        }

        private static readonly string LogAddress = "0x" + new string('0', 38) + "aa";

        private List<PersistableBlock> MakeChainedBlocksWithLogs(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlockWithLog(start + i));
            return list;
        }

        private PersistableBlock MakeBlockWithLog(long number)
        {
            var hash = Fill32(number);
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            var tx = MakeTx(number);

            var log = new Log
            {
                Address = LogAddress,
                Topics = new List<byte[]> { Topic((byte)((number % 7) + 1)) },
                Data = new byte[] { (byte)number },
            };
            var bloom = new LogBloomFilter();
            bloom.AddLog(log);

            var header = MakeHeader(number, parentHash);
            header.LogsBloom = bloom.Data;

            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log> { log } };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(receipt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: bloom.Data);
        }

        private static byte[] Topic(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static async Task WriteToHotAsync(RocksDbChainStoreBundle bundle, PersistableBlock block)
        {
            await bundle.Blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Transactions).ConfigureAwait(false);
            await bundle.Uncles.SaveAsync(block.Hash, block.Uncles).ConfigureAwait(false);
            await bundle.Withdrawals.SaveAsync(block.Hash, block.Withdrawals).ConfigureAwait(false);
            await bundle.Receipts.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Receipts).ConfigureAwait(false);
        }

        private List<PersistableBlock> MakeChainedBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private PersistableBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            var header = MakeHeader(number, parentHash);

            var tx = MakeTx(number);
            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(receipt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: new byte[256]);
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
            Timestamp = 1000 + number,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private ISignedTransaction MakeTx(long number)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: Array.Empty<byte>(),
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
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
