using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerReindexTests : IDisposable
    {
        private static readonly FilterMapsParams TinyParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 1,
            logValuesPerMap: 3,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerreindex_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private static readonly string LogAddress = "0x" + new string('0', 38) + "aa";

        private const long TipHeight = 90_010;
        private const int DrainCount = 16;
        private const long FullImmutabilityThreshold = 90_000;

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_FrozenBlocks_When_InlineFreeze_Then_ByHashAndFiltermapsBothBuilt()
        {
            var dataDir = Path.Combine(_root, "inline1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "inline1_freezer")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var probe = blocks[3];
            var resolved = await bundle.Blocks.GetByHashAsync(probe.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(probe.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());

            var fmStore = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests);
            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            var indexedHead = range.BlocksAfterLast - 1;
            Assert.True(indexedHead >= 0, "expected at least one indexed block");

            var filter = new LogFilter { Addresses = new List<string> { LogAddress }, FromBlock = 0, ToBlock = indexedHead };
            var results = await bundle.Logs.GetLogsAsync(filter);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Given_BackgroundFreezeIndexing_When_TheTrailerRuns_Then_ItAdvancesTheCursorToTheCommittedHead()
        {
            var dataDir = Path.Combine(_root, "trailer1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "trailer1_freezer"), backgroundIndexing: true, tinyFreezerFiles: true));
            using var bundle = OpenBundle(rocks, dataDir);

            bundle.StartBackgroundFreezeIndexing();

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            Assert.True(bundle.FreezerHead > 0, "expected frozen blocks");
            var expectedHead = bundle.FreezerHead;
            await WaitUntilAsync(() => bundle.ByHashIndexedHead >= expectedHead, TimeSpan.FromSeconds(15));
            Assert.Equal(expectedHead, bundle.ByHashIndexedHead);

            await Task.Delay(300);
            Assert.Equal(expectedHead, bundle.ByHashIndexedHead);

            var probe = blocks[3];
            var resolved = await bundle.Blocks.GetByHashAsync(probe.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(probe.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());

            var fmStore = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests);
            await WaitUntilAsync(() => fmStore.ReadRange() != null, TimeSpan.FromSeconds(15));
            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            var filter = new LogFilter { Addresses = new List<string> { LogAddress }, FromBlock = 0, ToBlock = range.BlocksAfterLast - 1 };
            Assert.NotEmpty(await bundle.Logs.GetLogsAsync(filter));
        }

        [Fact]
        public async Task Given_MoreBlocksKeepFreezing_When_TheTrailerRuns_Then_ItIndexesEveryCommittedItemIncludingTheActiveFile()
        {
            const int firstBatchSize = 8;
            const int secondBatchSize = 8;

            var dataDir = Path.Combine(_root, "trailer2");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "trailer2_freezer"), backgroundIndexing: true, tinyFreezerFiles: true));
            using var bundle = OpenBundle(rocks, dataDir);
            bundle.StartBackgroundFreezeIndexing();

            await SetTipHeightAsync(bundle, FullImmutabilityThreshold + firstBatchSize - 1);
            await bundle.PersistBlocksAsync(MakeBlocks(0, firstBatchSize));
            await WaitUntilAsync(() => bundle.ByHashIndexedHead >= bundle.FreezerHead, TimeSpan.FromSeconds(15));
            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);

            var firstHead = bundle.FreezerHead;
            await SetTipHeightAsync(bundle, FullImmutabilityThreshold + firstBatchSize + secondBatchSize - 1);
            await bundle.PersistBlocksAsync(MakeBlocks(firstBatchSize, secondBatchSize));
            Assert.True(bundle.FreezerHead > firstHead);
            await WaitUntilAsync(() => bundle.ByHashIndexedHead >= bundle.FreezerHead, TimeSpan.FromSeconds(15));
            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);
        }

        [Fact]
        public async Task Given_BackgroundFlagOnButTrailerNeverStarted_When_Freeze_Then_IndexesInlineNotDropped()
        {
            var dataDir = Path.Combine(_root, "fallback1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "fallback1_freezer"), backgroundIndexing: true));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var probe = blocks[3];
            var resolved = await bundle.Blocks.GetByHashAsync(probe.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(probe.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_TrailerStoppedByFinishBulkIndexing_When_FreezeAgain_Then_IndexesInlineNotDropped()
        {
            var dataDir = Path.Combine(_root, "stopped1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "stopped1_freezer"), backgroundIndexing: true));
            using var bundle = OpenBundle(rocks, dataDir);
            bundle.StartBackgroundFreezeIndexing();

            var firstBatch = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(firstBatch);

            bundle.FinishBulkIndexing();
            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);

            var secondBatch = MakeBlocks(DrainCount, DrainCount);
            await bundle.PersistBlocksAsync(secondBatch);

            var probe = secondBatch[3];
            var resolved = await bundle.Blocks.GetByHashAsync(probe.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(probe.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_FreezerIndexingPaused_When_Freeze_Then_FreezerHeadStillAdvances_ButIndexStaysBehind()
        {
            var dataDir = Path.Combine(_root, "paused1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "paused1_freezer"), backgroundIndexing: true));
            using var bundle = OpenBundle(rocks, dataDir);
            bundle.SetPressureMonitorForTests(new RocksDbWritePressureMonitor(
                Path.GetTempPath(), (_, __) => "0", (_, __) => "0", (_, __) => (100L * 1024 * 1024 * 1024).ToString()));
            bundle.StartBackgroundFreezeIndexing();

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            Assert.True(bundle.FreezerHead > 0, "freezer files must keep advancing while indexing is paused");
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.Equal(0, bundle.ByHashIndexedHead);
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.True(condition(), "trailer did not converge within timeout");
        }

        [Fact]
        public async Task Given_AMidArchiveByHashHole_When_TheFullPassRuns_Then_ItIsHealed()
        {
            var dataDir = Path.Combine(_root, "heal1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "heal1_freezer")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var hole = blocks[5];
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(hole.Hash));
            bundle.FreezerHistoryRocksForTests.Delete(HistoryColumnFamilies.BlockHashIndex, hole.Hash);
            Assert.Null(await bundle.Blocks.GetByHashAsync(hole.Hash));

            bundle.SetByHashReindexCursorForTests(0);

            bundle.FinishBulkIndexing();

            var healed = await bundle.Blocks.GetByHashAsync(hole.Hash);
            Assert.NotNull(healed);
            Assert.Equal(hole.Header.BlockNumber.ToBigInteger(), healed.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_AnInlineIndexedArchive_When_AlignByHashCursor_Then_CursorJumpsToHead_And_IsIdempotent()
        {
            var dataDir = Path.Combine(_root, "align1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "align1_freezer")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var head = bundle.FreezerHead;
            Assert.True(head > 0, "expected frozen blocks");
            Assert.Equal(head, bundle.ByHashIndexedHead);

            bundle.SetByHashReindexCursorForTests(0);
            Assert.Equal(0, bundle.ByHashIndexedHead);

            var (previous, updated) = bundle.AlignByHashReindexCursorToFreezerHead();
            Assert.Equal(0UL, previous);
            Assert.Equal((ulong)head, updated);
            Assert.Equal(head, bundle.ByHashIndexedHead);

            var (p2, u2) = bundle.AlignByHashReindexCursorToFreezerHead();
            Assert.Equal((ulong)head, p2);
            Assert.Equal((ulong)head, u2);
        }

        [Fact]
        public void Given_NoEpochCompleted_When_FreezeBatch_Then_NoCfFlush()
        {
            var dir = Path.Combine(_root, "dirtyflag");
            Directory.CreateDirectory(dir);
            using var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }, CatalogueScope.History);
            var store = new RocksDbFilterMapsStore(rocks);

            Assert.Equal(0, store.ColumnFamilyFlushCount);

            store.BeginBulk();
            store.EndBulk();

            Assert.Equal(0, store.ColumnFamilyFlushCount);
        }

        [Fact]
        public async Task Given_AnUnindexedFreezerArchiveWithNoTrailerEverStarted_When_FinishBulkIndexingRuns_Then_ItIsFullyIndexed()
        {
            var dataDir = Path.Combine(_root, "uploaded1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "uploaded1_freezer")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var freezerHistoryRocks = bundle.FreezerHistoryRocksForTests;
            ClearColumnFamily(freezerHistoryRocks, HistoryColumnFamilies.BlockHashIndex);
            ClearColumnFamily(freezerHistoryRocks, HistoryColumnFamilies.TxHashIndex);
            ClearColumnFamily(freezerHistoryRocks, HistoryColumnFamilies.LogFilterMaps);
            bundle.SetByHashReindexCursorForTests(0);

            var probe = blocks[3];
            Assert.Null(await bundle.Blocks.GetByHashAsync(probe.Hash));

            bundle.FinishBulkIndexing();

            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);
            var resolved = await bundle.Blocks.GetByHashAsync(probe.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(probe.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());

            for (var i = 0; i < probe.Transactions.Count; i++)
            {
                var location = await bundle.Transactions.GetLocationAsync(probe.Transactions[i].Hash);
                Assert.NotNull(location);
                Assert.Equal(i, location.TransactionIndex);
            }

            var fmStore = new RocksDbFilterMapsStore(freezerHistoryRocks);
            var range = fmStore.ReadRange();
            Assert.NotNull(range);

            var filter = new LogFilter { Addresses = new List<string> { LogAddress }, FromBlock = 0, ToBlock = range.BlocksAfterLast - 1 };
            var results = await bundle.Logs.GetLogsAsync(filter);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Given_TheTrailerHasCaughtUp_When_FinishBulkIndexingRuns_Then_ItFinalizesWithTheWholeHeadIndexed()
        {
            var dataDir = Path.Combine(_root, "finalfile1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "finalfile1_freezer"), backgroundIndexing: true, tinyFreezerFiles: true));
            using var bundle = OpenBundle(rocks, dataDir);
            bundle.StartBackgroundFreezeIndexing();

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            await WaitUntilAsync(() => bundle.ByHashIndexedHead >= bundle.FreezerHead, TimeSpan.FromSeconds(15));

            bundle.FinishBulkIndexing();

            Assert.Equal(bundle.FreezerHead, bundle.ByHashIndexedHead);
            var tip = blocks[(int)(bundle.FreezerHead - 1)];
            var resolved = await bundle.Blocks.GetByHashAsync(tip.Hash);
            Assert.NotNull(resolved);
            Assert.Equal(tip.Header.BlockNumber.ToBigInteger(), resolved.BlockNumber.ToBigInteger());
        }

        private const long MultiRecordFreezerFileSizeBytes = 700;
        private const int MultiRecordBlockCount = 20;

        [Fact]
        public async Task Given_AFixtureWithTinyMaxFileSizeForcingMultiRecordChunks_When_StandaloneBulkIndexerRuns_Then_KnownBlockAndTxHashResolve_AndChunkPathWasUsed()
        {
            var dataDir = Path.Combine(_root, "chunkpath");
            Directory.CreateDirectory(dataDir);
            var freezerDir = Path.Combine(_root, "chunkpath_freezer");
            var blocks = MakeBlocks(0, MultiRecordBlockCount);

            using (var buildBundle = OpenBundle(
                new RocksDbManager(FreezerOptions(dataDir, freezerDir, freezerMaxFileSizeBytes: MultiRecordFreezerFileSizeBytes)),
                dataDir))
            {
                await SetTipHeightAsync(buildBundle, FullImmutabilityThreshold + MultiRecordBlockCount - 1);
                await buildBundle.PersistBlocksAsync(blocks);
            }

            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDir, MultiRecordFreezerFileSizeBytes), Nethereum.Freezer.FreezerOpenMode.ReadOnly);

            var bodiesFileNumbers = new List<ushort>();
            for (var b = 0L; b < MultiRecordBlockCount; b++)
                bodiesFileNumbers.Add(freezer.FileNumberOf("bodies", b));
            Assert.True(bodiesFileNumbers[0] != bodiesFileNumbers[MultiRecordBlockCount - 1],
                "fixture must span more than one 'bodies' file");
            var maxRunLength = 1;
            var runLength = 1;
            for (var i = 1; i < bodiesFileNumbers.Count; i++)
            {
                runLength = bodiesFileNumbers[i] == bodiesFileNumbers[i - 1] ? runLength + 1 : 1;
                if (runLength > maxRunLength) maxRunLength = runLength;
            }
            Assert.True(maxRunLength > 1, "fixture must pack more than one item into at least one 'bodies' file");

            var indexDir = Path.Combine(_root, "chunkpath_index");
            Directory.CreateDirectory(indexDir);
            var codecs = new Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet();
            using var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = indexDir }, CatalogueScope.FreezerHistory);

            var progress = new Nethereum.CoreChain.RocksDB.Freezer.FreezerHistoryIndexProgress(
                indexRocks, HistoryColumnFamilies.Control);
            var indexLock = new object();
            var ingestor = new BulkIndexIngestor(
                indexRocks.Database, Path.Combine(indexDir, "bulk-scratch"),
                new[] { HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.TxHashIndex },
                windowHead => { lock (indexLock) progress.SetByHashCursor(windowHead + 1); },
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 100);
            var fmStore = new RocksDbFilterMapsStore(indexRocks);
            var chainView = new Nethereum.CoreChain.Freezer.FilterMaps.SegmentChainView(freezer, codecs);
            var finality = new Nethereum.CoreChain.Freezer.FilterMaps.FreezerHeadFinalitySource(freezer);
            var fmIndexer = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer(fmStore, chainView, finality, TinyParams);
            var builder = new Nethereum.CoreChain.RocksDB.Freezer.FreezerBulkIndexBuilder(
                freezer, codecs, ingestor, fmStore, fmIndexer, progress);

            var head = builder.ComputeIndexableBoundary();
            Assert.True(head > 0, "the freezer should expose committed frozen blocks to index");

            builder.CatchUpByHash(head, System.Threading.CancellationToken.None);
            builder.RenderFilterMaps(headOverride: head);
            ingestor.Finish();

            var blockProbe = blocks[3];
            Assert.True(new RocksDbRandomKeyIndexStore(indexRocks).TryGetBlockNumberByHash(blockProbe.Hash, out var number),
                "a known block hash must resolve after the chunk-path standalone build");
            Assert.Equal(blockProbe.Header.BlockNumber.ToBigInteger(), (BigInteger)number);

            var txProbe = blocks[3].Transactions[1];
            var txLoc = indexRocks.Database.Get(txProbe.Hash, indexRocks.GetColumnFamily(HistoryColumnFamilies.TxHashIndex));
            Assert.NotNull(txLoc);
            Assert.True(txLoc.Length >= Nethereum.CoreChain.Storage.History.HistoryKeys.TxKeyLength,
                "a known transaction hash must resolve after the chunk-path standalone build");
            Assert.Equal(blockProbe.Header.BlockNumber.ToBigInteger(), (BigInteger)Nethereum.CoreChain.Storage.History.HistoryKeys.ReadBlockNumber(txLoc));
            Assert.Equal(1u, Nethereum.CoreChain.Storage.History.HistoryKeys.ReadTxIndex(txLoc));

            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            Assert.True(range.BlocksAfterLast > 0, "the standalone render must advance the filter-maps index over the chunk-read fixture");
        }

        [Fact]
        public async Task Given_HeadOverrunsWhatTheFreezerHolds_When_CatchUpByHashRuns_Then_ThrowsFreezerConsistencyException()
        {
            var dataDir = Path.Combine(_root, "noprogress");
            Directory.CreateDirectory(dataDir);
            var freezerDir = Path.Combine(_root, "noprogress_freezer");
            var blocks = MakeBlocks(0, DrainCount);

            using (var buildBundle = OpenBundle(new RocksDbManager(FreezerOptions(dataDir, freezerDir)), dataDir))
            {
                await SetTipHeightAsync(buildBundle, TipHeight);
                await buildBundle.PersistBlocksAsync(blocks);
            }

            var indexDir = Path.Combine(_root, "noprogress_index");
            Directory.CreateDirectory(indexDir);
            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDir), Nethereum.Freezer.FreezerOpenMode.ReadOnly);
            var codecs = new Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet();
            using var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = indexDir }, CatalogueScope.FreezerHistory);

            var progress = new Nethereum.CoreChain.RocksDB.Freezer.FreezerHistoryIndexProgress(
                indexRocks, HistoryColumnFamilies.Control);
            var ingestor = new BulkIndexIngestor(
                indexRocks.Database, Path.Combine(indexDir, "bulk-scratch"),
                new[] { HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.TxHashIndex },
                windowHead => progress.SetByHashCursor(windowHead + 1),
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 100);
            var builder = new Nethereum.CoreChain.RocksDB.Freezer.FreezerBulkIndexBuilder(
                freezer, codecs, ingestor, fmStore: null, fmIndexer: null, progress);

            var actualHead = freezer.Items;
            var overrunHead = actualHead + 100;

            var ex = Assert.Throws<Nethereum.Freezer.FreezerConsistencyException>(
                () => builder.CatchUpByHash(overrunHead, System.Threading.CancellationToken.None));
            Assert.Contains("no progress", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_BothJobsHaveWorkAndRunConcurrently_Then_TheyOverlapInWallClockTime_AndBothCursorsReachCapturedHead()
        {
            var dataDir = Path.Combine(_root, "concurrent");
            Directory.CreateDirectory(dataDir);
            var freezerDir = Path.Combine(_root, "concurrent_freezer");
            const int blockCount = 200;
            var blocks = MakeBlocks(0, blockCount);

            using (var buildBundle = OpenBundle(
                new RocksDbManager(FreezerOptions(dataDir, freezerDir, freezerMaxFileSizeBytes: MultiRecordFreezerFileSizeBytes)),
                dataDir))
            {
                await SetTipHeightAsync(buildBundle, FullImmutabilityThreshold + blockCount - 1);
                await buildBundle.PersistBlocksAsync(blocks);
            }

            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDir, MultiRecordFreezerFileSizeBytes), Nethereum.Freezer.FreezerOpenMode.ReadOnly);
            var codecs = new Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet();

            var indexDir = Path.Combine(_root, "concurrent_index");
            Directory.CreateDirectory(indexDir);
            using var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = indexDir }, CatalogueScope.FreezerHistory);

            var progress = new Nethereum.CoreChain.RocksDB.Freezer.FreezerHistoryIndexProgress(
                indexRocks, HistoryColumnFamilies.Control);
            var indexLock = new object();
            var ingestor = new BulkIndexIngestor(
                indexRocks.Database, Path.Combine(indexDir, "bulk-scratch"),
                new[] { HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.TxHashIndex },
                windowHead => { lock (indexLock) progress.SetByHashCursor(windowHead + 1); },
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 1);
            var fmStore = new RocksDbFilterMapsStore(indexRocks);
            var chainView = new Nethereum.CoreChain.Freezer.FilterMaps.SegmentChainView(freezer, codecs);
            var finality = new Nethereum.CoreChain.Freezer.FilterMaps.FreezerHeadFinalitySource(freezer);
            var fmIndexer = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer(fmStore, chainView, finality, TinyParams);
            var builder = new Nethereum.CoreChain.RocksDB.Freezer.FreezerBulkIndexBuilder(
                freezer, codecs, ingestor, fmStore, fmIndexer, progress);

            var head = builder.ComputeIndexableBoundary();
            Assert.True(head > 0, "the freezer should expose committed frozen blocks to index");

            (DateTime Start, DateTime End) byHashInterval = default;
            (DateTime Start, DateTime End) renderInterval = default;

            var byHashTask = Task.Factory.StartNew(() =>
            {
                var start = DateTime.UtcNow;
                builder.CatchUpByHash(head, System.Threading.CancellationToken.None);
                byHashInterval = (start, DateTime.UtcNow);
            }, System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var renderTask = Task.Factory.StartNew(() =>
            {
                var start = DateTime.UtcNow;
                builder.RenderFilterMaps(headOverride: head);
                renderInterval = (start, DateTime.UtcNow);
            }, System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Task.WaitAll(byHashTask, renderTask);

            Assert.True(
                byHashInterval.Start < renderInterval.End && renderInterval.Start < byHashInterval.End,
                $"expected wall-clock overlap: byHash=[{byHashInterval.Start:O},{byHashInterval.End:O}] " +
                $"render=[{renderInterval.Start:O},{renderInterval.End:O}]");

            Assert.Equal(head, builder.ByHashIndexedHead);
            Assert.True(builder.LogIndexRenderedHead > 0, "the concurrent render must still advance the filter-maps index");
        }

        [Fact]
        public async Task Given_FrozenFreezerFiles_When_StandaloneBulkIndexerRuns_Then_ByHashAndFiltermapsBuilt()
        {
            var dataDir = Path.Combine(_root, "standalone");
            Directory.CreateDirectory(dataDir);
            var freezerDir = Path.Combine(_root, "standalone_freezer");
            var blocks = MakeBlocks(0, DrainCount);

            using (var buildBundle = OpenBundle(new RocksDbManager(FreezerOptions(dataDir, freezerDir)), dataDir))
            {
                await SetTipHeightAsync(buildBundle, TipHeight);
                await buildBundle.PersistBlocksAsync(blocks);
            }

            var indexDir = Path.Combine(_root, "standalone_index");
            Directory.CreateDirectory(indexDir);
            using var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(freezerDir), Nethereum.Freezer.FreezerOpenMode.ReadOnly);
            var codecs = new Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet();
            using var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = indexDir }, CatalogueScope.FreezerHistory);

            var progress = new Nethereum.CoreChain.RocksDB.Freezer.FreezerHistoryIndexProgress(
                indexRocks, HistoryColumnFamilies.Control);
            var indexLock = new object();
            var ingestor = new BulkIndexIngestor(
                indexRocks.Database, Path.Combine(indexDir, "bulk-scratch"),
                new[] { HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.TxHashIndex },
                windowHead => { lock (indexLock) progress.SetByHashCursor(windowHead + 1); },
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 100);
            var fmStore = new RocksDbFilterMapsStore(indexRocks);
            var chainView = new Nethereum.CoreChain.Freezer.FilterMaps.SegmentChainView(freezer, codecs);
            var finality = new Nethereum.CoreChain.Freezer.FilterMaps.FreezerHeadFinalitySource(freezer);
            var fmIndexer = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer(fmStore, chainView, finality, TinyParams);
            var builder = new Nethereum.CoreChain.RocksDB.Freezer.FreezerBulkIndexBuilder(
                freezer, codecs, ingestor, fmStore, fmIndexer, progress);

            var head = builder.ComputeIndexableBoundary();
            Assert.True(head > 0, "the freezer should expose committed frozen blocks to index");

            var frozenProbe = blocks[3];
            Assert.False(new RocksDbRandomKeyIndexStore(indexRocks).TryGetBlockNumberByHash(frozenProbe.Hash, out _),
                "twin: a fresh index must MISS before the standalone build runs");

            builder.CatchUpByHash(head, System.Threading.CancellationToken.None);
            builder.RenderFilterMaps(headOverride: head);
            ingestor.Finish();

            Assert.True(new RocksDbRandomKeyIndexStore(indexRocks).TryGetBlockNumberByHash(frozenProbe.Hash, out var number),
                "the standalone build must resolve a frozen block by hash");
            Assert.Equal(frozenProbe.Header.BlockNumber.ToBigInteger(), (BigInteger)number);

            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            Assert.True(range.BlocksAfterLast > 0, "the standalone render must advance the filter-maps index");
        }

        private const long TinyFreezerFileSizeBytes = 8;

        private RocksDbStorageOptions FreezerOptions(
            string dataDir, string freezerDir, bool backgroundIndexing = false, bool tinyFreezerFiles = false,
            long? freezerMaxFileSizeBytes = null) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            UseFreezerHistory = true,
            FreezerHistoryDirectory = freezerDir,
            FilterMapsIndexParams = TinyParams,
            BackgroundFreezeIndexing = backgroundIndexing,
            FreezerMaxFileSizeBytes = freezerMaxFileSizeBytes ?? (tinyFreezerFiles ? TinyFreezerFileSizeBytes : (long?)null),
            FreezerCommitCadenceBlocks = 1,
        };

        private RocksDbChainStoreBundle OpenBundle(RocksDbManager rocks, string dataDir)
            => RocksDbChainStoreBundle.FromManager(
                rocks, dataDir, journalOptions: null, ownsManager: true, bulkSync: false,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: _signer);

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        private List<PersistableBlock> MakeBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private PersistableBlock MakeBlock(long number)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);
            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            var receipts = new List<ReceiptSaveItem>();
            BigInteger cumulative = 0;
            for (var j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = LogAddress, Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
            }

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: Fill(0x00, 256));
        }

        private static BlockHeader MakeHeader(long number, byte[] hash) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = new byte[32],
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private ISignedTransaction MakeTx(long number, int index)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (var i = 0; i < len; i++) b[i] = v;
            return b;
        }

        private static void ClearColumnFamily(RocksDbManager manager, string cf)
        {
            var keys = new List<byte[]>();
            using (var it = manager.CreateIterator(cf))
            {
                it.SeekToFirst();
                while (it.Valid())
                {
                    keys.Add(it.Key());
                    it.Next();
                }
            }
            foreach (var key in keys)
                manager.Delete(cf, key);
        }
    }
}
