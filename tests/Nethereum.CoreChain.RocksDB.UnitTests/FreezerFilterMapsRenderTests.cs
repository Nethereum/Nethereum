using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerFilterMapsRenderTests : IDisposable
    {
        private static readonly FilterMapsParams TinyParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 1,
            logValuesPerMap: 3,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        private static readonly FilterMapsParams BigEpochParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 3,
            logValuesPerMap: 5,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"fmrender_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long TipHeight = 90_010;
        private const long FreezeBoundary = 10;
        private const int DrainCount = 16;
        private static readonly string LogAddress = "0x" + new string('0', 38) + "aa";

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_FrozenBlocksWithLogs_When_RenderCatchUp_Then_FmIndexPopulatedAndQueryable()
        {
            var dataDir = Path.Combine(_root, "data1");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "freezer1")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var fmStore = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests);
            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            var indexedHead = range.BlocksAfterLast - 1;
            Assert.True(indexedHead >= 0, "expected at least one indexed block");
            Assert.True(indexedHead <= FreezeBoundary, "must never index past the freezer head");

            var chainView = new BundleChainView(bundle);
            var engine = new FilterMapsQueryEngine(
                fmStore,
                new FilterMapsMatcher(new FilterMapsQueryBackend(fmStore, TinyParams)),
                new FilterMapsLogResolver(fmStore, chainView, TinyParams),
                new EmptyHistoricalLogScan());

            var filter = new LogFilter { Addresses = new List<string> { LogAddress }, FromBlock = 0, ToBlock = indexedHead };
            var results = await engine.GetLogsAsync(filter);

            Assert.NotEmpty(results);
            foreach (var r in results)
            {
                Assert.True(r.Log.Address.IsTheSameAddress(LogAddress));
                Assert.InRange(r.BlockNumber, 0, indexedHead);
            }

            var expectedCount = 0;
            for (var b = 0L; b <= indexedHead; b++)
            {
                var receipts = await bundle.Receipts.GetByBlockNumberAsync(b);
                foreach (var rcpt in receipts) expectedCount += rcpt.Logs.Count;
            }
            Assert.Equal(expectedCount, results.Count);
        }

        [Fact]
        public void Given_BulkAndNonBulkRender_When_ReadBack_Then_ByteIdentical()
        {
            var chain = BuildSyntheticChain(40);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirBulk = Path.Combine(_root, "bulk");
            var dirPlain = Path.Combine(_root, "plain");
            Directory.CreateDirectory(dirBulk);
            Directory.CreateDirectory(dirPlain);

            using var rocksBulk = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBulk }, CatalogueScope.History);
            using var rocksPlain = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirPlain }, CatalogueScope.History);

            var storeBulk = new RocksDbFilterMapsStore(rocksBulk);
            var storePlain = new RocksDbFilterMapsStore(rocksPlain);

            var indexerBulk = new FilterMapsIndexer(storeBulk, chain, finality, TinyParams);
            var indexerPlain = new FilterMapsIndexer(storePlain, chain, finality, TinyParams);

            storeBulk.BeginBulk();
            var epochsBulk = 0;
            while (indexerBulk.RenderHead()) epochsBulk++;
            storeBulk.EndBulk();

            var epochsPlain = 0;
            while (indexerPlain.RenderHead()) epochsPlain++;

            Assert.True(epochsBulk > 0, "expected the synthetic chain to render at least one epoch");
            Assert.Equal(epochsPlain, epochsBulk);

            AssertColumnFamilyByteIdentical(rocksBulk, rocksPlain, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public void Given_AnEpochSplitAcrossChunkBoundaries_When_RenderedInPieces_Then_MapRowsAreByteIdenticalToWholeEpochRender()
        {
            const int blockCount = 20;
            const long splitIndex = 2;

            var chain = BuildSyntheticChain(blockCount);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirWhole = Path.Combine(_root, "epochsplit_whole");
            var dirSplit = Path.Combine(_root, "epochsplit_split");
            Directory.CreateDirectory(dirWhole);
            Directory.CreateDirectory(dirSplit);

            using var rocksWhole = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirWhole }, CatalogueScope.History);
            using var rocksSplit = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirSplit }, CatalogueScope.History);

            var storeWhole = new RocksDbFilterMapsStore(rocksWhole);
            var storeSplit = new RocksDbFilterMapsStore(rocksSplit);

            var indexerWhole = new FilterMapsIndexer(storeWhole, chain, finality, TinyParams);
            var epochBoundaries = new List<long>();
            while (indexerWhole.RenderHead())
                epochBoundaries.Add(storeWhole.ReadRange().BlocksAfterLast);

            Assert.True(epochBoundaries.Count >= 2, "fixture must span at least two complete epochs");
            Assert.True(epochBoundaries[0] > splitIndex + 1,
                "the chosen split must land strictly before the first epoch's own completion boundary, " +
                "otherwise the resume path below is never exercised");

            var indexerSplit = new FilterMapsIndexer(storeSplit, chain, finality, TinyParams);
            var chunk1 = ChunkOf(chain, 0, splitIndex);
            var chunk2 = ChunkOf(chain, splitIndex + 1, blockCount - 1);

            var epochsChunk1 = indexerSplit.RenderChunk(chunk1, finalizedBlockBound: blockCount - 1);
            var epochsChunk2 = indexerSplit.RenderChunk(chunk2, finalizedBlockBound: blockCount - 1);

            Assert.Equal(0, epochsChunk1);
            Assert.True(epochsChunk2 > 0, "chunk 2 must have completed the epoch that chunk 1 left pending");
            Assert.Equal(epochBoundaries.Count, epochsChunk1 + epochsChunk2);

            var rangeWhole = storeWhole.ReadRange();
            var rangeSplit = storeSplit.ReadRange();
            Assert.NotNull(rangeWhole);
            Assert.NotNull(rangeSplit);
            Assert.Equal(rangeWhole.Encode(), rangeSplit.Encode());

            AssertColumnFamilyByteIdentical(rocksWhole, rocksSplit, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public void Given_AnEpochRenderedWithDop_GreaterThanOne_When_ComparedToSerialDop1Render_Then_AllFilterMapCfRowsAreByteIdentical()
        {
            var chain = BuildSyntheticChain(40);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirDop1 = Path.Combine(_root, "dop_serial");
            var dirDopN = Path.Combine(_root, "dop_parallel");
            Directory.CreateDirectory(dirDop1);
            Directory.CreateDirectory(dirDopN);

            using var rocksDop1 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirDop1 }, CatalogueScope.History);
            using var rocksDopN = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirDopN }, CatalogueScope.History);

            var storeDop1 = new RocksDbFilterMapsStore(rocksDop1);
            var storeDopN = new RocksDbFilterMapsStore(rocksDopN);

            var indexerDop1 = new FilterMapsIndexer(storeDop1, chain, finality, TinyParams, renderDegreeOfParallelism: 1);
            var indexerDopN = new FilterMapsIndexer(storeDopN, chain, finality, TinyParams, renderDegreeOfParallelism: 4);

            var epochsDop1 = 0;
            while (indexerDop1.RenderHead()) epochsDop1++;
            var epochsDopN = 0;
            while (indexerDopN.RenderHead()) epochsDopN++;

            Assert.True(epochsDop1 > 0, "expected the synthetic chain to render at least one epoch");
            Assert.Equal(epochsDop1, epochsDopN);
            Assert.True(indexerDopN.MaxParallelMapBatchSize >= 2,
                "non-vacuity: the dop>1 path must have dispatched at least 2 maps into the same Parallel.For batch");

            AssertColumnFamilyByteIdentical(rocksDop1, rocksDopN, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public void Given_ParallelEmitHashingDopGreaterThanOne_When_ComparedToSerialDop1_Then_ByteIdentical()
        {
            var chain = BuildSyntheticChain(40);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirDop1 = Path.Combine(_root, "emit_serial");
            var dirDopN = Path.Combine(_root, "emit_parallel");
            Directory.CreateDirectory(dirDop1);
            Directory.CreateDirectory(dirDopN);

            using var rocksDop1 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirDop1 }, CatalogueScope.History);
            using var rocksDopN = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirDopN }, CatalogueScope.History);

            var storeDop1 = new RocksDbFilterMapsStore(rocksDop1);
            var storeDopN = new RocksDbFilterMapsStore(rocksDopN);

            var indexerDop1 = new FilterMapsIndexer(storeDop1, chain, finality, TinyParams, renderDegreeOfParallelism: 1);
            var indexerDopN = new FilterMapsIndexer(storeDopN, chain, finality, TinyParams, renderDegreeOfParallelism: 4);

            var epochsDop1 = 0;
            while (indexerDop1.RenderHead()) epochsDop1++;
            var epochsDopN = 0;
            while (indexerDopN.RenderHead()) epochsDopN++;

            Assert.True(epochsDop1 > 0, "expected the synthetic chain to render at least one epoch");
            Assert.Equal(epochsDop1, epochsDopN);
            Assert.Equal(0L, indexerDop1.ValuesHashedByParallelEmit);
            Assert.True(indexerDopN.ValuesHashedByParallelEmit > 0,
                "non-vacuity: the dop>1 path must have hashed at least one log value through the parallel emit pre-pass");

            AssertColumnFamilyByteIdentical(rocksDop1, rocksDopN, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public void Given_MultiEpochRenderHeadWithDopGreaterThanOne_When_Summed_Then_EachValueIsHashedAtMostOnceEver()
        {
            const int blockCount = 40;
            const int valuesPerBlock = 2;

            var chain = BuildSyntheticChain(blockCount);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dir = Path.Combine(_root, "emit_bound");
            Directory.CreateDirectory(dir);

            using var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }, CatalogueScope.History);
            var store = new RocksDbFilterMapsStore(rocks);
            var indexer = new FilterMapsIndexer(store, chain, finality, TinyParams, renderDegreeOfParallelism: 4);

            var epochs = 0;
            while (indexer.RenderHead()) epochs++;

            Assert.True(epochs > 1, "fixture must span more than one epoch to exercise repeated RenderHead calls");
            Assert.True(indexer.ValuesHashedByParallelEmit > 0,
                "non-vacuity: the dop>1 path must have hashed at least one log value through the parallel emit pre-pass");
            Assert.True(indexer.ValuesHashedByParallelEmit <= indexer.LogValueLowerBound,
                "the emit must never hash more values than Pass A's own walk actually visited across all RenderHead calls " +
                "-- a bound of O(walked values), not O(epochs * remaining range)");
            Assert.True(indexer.ValuesHashedByParallelEmit < blockCount * valuesPerBlock * 2,
                "hashed count must stay within a small constant of the true log-value count, not balloon with epoch count");
        }

        [Fact]
        public void Given_AMapStraddlingAChunkBoundary_When_RenderedWithDopGreaterThanOne_Then_ByteIdenticalToSerialWholeRender()
        {
            const int blockCount = 20;

            var chain = BuildSyntheticChain(blockCount);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirWhole = Path.Combine(_root, "mapstraddle_whole");
            var dirSplit = Path.Combine(_root, "mapstraddle_split");
            Directory.CreateDirectory(dirWhole);
            Directory.CreateDirectory(dirSplit);

            using var rocksWhole = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirWhole }, CatalogueScope.History);
            using var rocksSplit = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirSplit }, CatalogueScope.History);

            var storeWhole = new RocksDbFilterMapsStore(rocksWhole);
            var storeSplit = new RocksDbFilterMapsStore(rocksSplit);

            var indexerWhole = new FilterMapsIndexer(storeWhole, chain, finality, TinyParams);
            while (indexerWhole.RenderHead()) { }

            var straddleMap = storeWhole.ReadLastBlockOfMap(0);
            Assert.True(straddleMap.HasValue, "map 0 must have been rendered by the reference whole render");
            Assert.True(straddleMap.Value.blockNumber > 0,
                "the split below needs at least one earlier block already inside map 0 for a genuine straddle");

            var splitIndex = straddleMap.Value.blockNumber - 1;

            var indexerSplit = new FilterMapsIndexer(storeSplit, chain, finality, TinyParams, renderDegreeOfParallelism: 4);
            var chunk1 = ChunkOf(chain, 0, splitIndex);
            var chunk2 = ChunkOf(chain, splitIndex + 1, blockCount - 1);

            var epochsChunk1 = indexerSplit.RenderChunk(chunk1, finalizedBlockBound: blockCount - 1);
            var epochsChunk2 = indexerSplit.RenderChunk(chunk2, finalizedBlockBound: blockCount - 1);

            Assert.Equal(0, epochsChunk1);
            Assert.True(epochsChunk2 > 0, "chunk 2 must complete the epoch that chunk 1 left the straddling map open in");

            var rangeWhole = storeWhole.ReadRange();
            var rangeSplit = storeSplit.ReadRange();
            Assert.NotNull(rangeWhole);
            Assert.NotNull(rangeSplit);
            Assert.Equal(rangeWhole.Encode(), rangeSplit.Encode());

            AssertColumnFamilyByteIdentical(rocksWhole, rocksSplit, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public void Given_LiveRenderDop1_When_RenderHead_Then_OutputUnchangedFromBaseline()
        {
            var chain = BuildSyntheticChain(40);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dirOmitted = Path.Combine(_root, "livedefault_omitted");
            var dirExplicit = Path.Combine(_root, "livedefault_explicit");
            Directory.CreateDirectory(dirOmitted);
            Directory.CreateDirectory(dirExplicit);

            using var rocksOmitted = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirOmitted }, CatalogueScope.History);
            using var rocksExplicit = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirExplicit }, CatalogueScope.History);

            var storeOmitted = new RocksDbFilterMapsStore(rocksOmitted);
            var storeExplicit = new RocksDbFilterMapsStore(rocksExplicit);

            var indexerOmitted = new FilterMapsIndexer(storeOmitted, chain, finality, TinyParams);
            var indexerExplicit = new FilterMapsIndexer(storeExplicit, chain, finality, TinyParams, renderDegreeOfParallelism: 1);

            Assert.Equal(1, indexerOmitted.RenderDegreeOfParallelism);

            var epochsOmitted = 0;
            while (indexerOmitted.RenderHead()) epochsOmitted++;
            var epochsExplicit = 0;
            while (indexerExplicit.RenderHead()) epochsExplicit++;

            Assert.True(epochsOmitted > 0, "expected the synthetic chain to render at least one epoch");
            Assert.Equal(epochsExplicit, epochsOmitted);

            AssertColumnFamilyByteIdentical(rocksOmitted, rocksExplicit, HistoryColumnFamilies.LogFilterMaps);
        }

        [Fact]
        public async Task Given_RenderCatchUp_When_CalledTwice_Then_SecondIsNoOp()
        {
            var dataDir = Path.Combine(_root, "data3");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "freezer3")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(blocks);

            var fmStore = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests);
            var rangeAfterFreeze = fmStore.ReadRange();
            Assert.NotNull(rangeAfterFreeze);

            var first = bundle.RenderFilterMapsCatchUp();
            Assert.Equal(0, first);

            var rangeAfterFirst = fmStore.ReadRange();
            Assert.NotNull(rangeAfterFirst);
            Assert.Equal(rangeAfterFreeze.Encode(), rangeAfterFirst.Encode());

            var second = bundle.RenderFilterMapsCatchUp();
            Assert.Equal(0, second);

            var rangeAfterSecond = fmStore.ReadRange();
            Assert.NotNull(rangeAfterSecond);
            Assert.Equal(rangeAfterFirst.Encode(), rangeAfterSecond.Encode());
        }

        [Fact]
        public void Given_BulkRender_When_EndBulkRuns_Then_TheColumnFamilyIsFlushed()
        {
            var chain = BuildSyntheticChain(40);
            var finality = new FixedFinality { FinalizedBlockNumber = chain.HeadNumber };

            var dir = Path.Combine(_root, "flushcheck");
            Directory.CreateDirectory(dir);
            using var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir }, CatalogueScope.History);
            var store = new RocksDbFilterMapsStore(rocks);
            var indexer = new FilterMapsIndexer(store, chain, finality, TinyParams);

            Assert.Equal(0, store.ColumnFamilyFlushCount);

            store.BeginBulk();
            while (indexer.RenderHead()) { }
            store.EndBulk();

            Assert.True(store.ColumnFamilyFlushCount >= 1,
                "EndBulk must issue at least one real column-family flush -- the actual durability checkpoint (FIX 1)");
        }

        [Fact]
        public async Task Given_AFailureMidRender_When_RenderCatchUp_Then_TheOriginalExceptionPropagatesAndTheStoreRecovers()
        {
            var dataDir = Path.Combine(_root, "data5");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "freezer5")));
            using var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks(0, DrainCount);
            await SetTipHeightAsync(bundle, TipHeight);

            var scratchPath = Path.Combine(dataDir, RocksDbChainStoreBundle.FreezerHistorySubDir, "fm-sst-scratch");
            File.WriteAllBytes(scratchPath, new byte[] { 0 });

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => bundle.PersistBlocksAsync(blocks));
            Assert.IsType<IOException>(ex);

            File.Delete(scratchPath);
            var epochs = bundle.RenderFilterMapsCatchUp();
            Assert.True(epochs > 0, "expected the store to recover and render normally once the transient failure clears");
        }

        [Fact]
        public async Task Given_FrozenRangeBelowOneEpoch_When_Persist_Then_GateSkipsRenderButCountsLogValues()
        {
            var dataDir = Path.Combine(_root, "gate_below");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, "gate_below_fz"), BigEpochParams));
            using var bundle = OpenBundle(rocks, dataDir);

            await SetTipHeightAsync(bundle, TipHeight);
            await bundle.PersistBlocksAsync(MakeBlocks(0, DrainCount));

            var range = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests).ReadRange();
            Assert.True(range == null || range.BlocksAfterLast == 0, "below one epoch, the gate must render nothing");
            Assert.Equal(0, bundle.RenderFilterMapsCatchUp());

            var raw = bundle.FreezerHistoryRocksForTests.Get(Nethereum.CoreChain.RocksDB.History.HistoryColumnFamilies.Control,
                System.Text.Encoding.ASCII.GetBytes("filtermaps_lv_progress"));
            Assert.NotNull(raw);
            Assert.Equal(16, raw.Length);
            Assert.Equal(44L, (long)RocksDbManager.Read64BE(raw.AsSpan(0, 8).ToArray()));
            Assert.Equal(11L, (long)RocksDbManager.Read64BE(raw.AsSpan(8, 8).ToArray()));
        }

        private RocksDbStorageOptions FreezerOptions(string dataDir, string freezerDir)
            => FreezerOptions(dataDir, freezerDir, TinyParams);

        private RocksDbStorageOptions FreezerOptions(string dataDir, string freezerDir, FilterMapsParams fmParams) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            UseFreezerHistory = true,
            FreezerHistoryDirectory = freezerDir,
            FilterMapsIndexParams = fmParams,
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

        private static SyntheticChainView BuildSyntheticChain(int blockCount)
        {
            var chain = new SyntheticChainView();
            for (var b = 0; b < blockCount; b++)
            {
                var log = new Log
                {
                    Address = Address((byte)(b % 7 + 1)),
                    Data = null,
                    Topics = new List<byte[]> { Topic((byte)(b % 5 + 1)) },
                };
                chain.SetBlock(b, log);
            }
            return chain;
        }

        private static string Address(byte seed)
        {
            var bytes = new byte[20];
            bytes[19] = seed;
            return bytes.ToHex(true);
        }

        private static byte[] Topic(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static List<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)> ChunkOf(
            SyntheticChainView chain, long fromInclusive, long toInclusive)
        {
            var list = new List<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)>();
            for (var b = fromInclusive; b <= toInclusive; b++)
                list.Add((b, chain.Receipts(b)));
            return list;
        }

        private static void AssertColumnFamilyByteIdentical(RocksDbManager a, RocksDbManager b, string cf)
        {
            var da = DumpColumnFamily(a, cf);
            var db = DumpColumnFamily(b, cf);
            Assert.Equal(da.Count, db.Count);
            foreach (var kv in da)
            {
                Assert.True(db.TryGetValue(kv.Key, out var otherValue), $"key present only on one side: {kv.Key}");
                Assert.Equal(kv.Value, otherValue);
            }
        }

        private static Dictionary<string, byte[]> DumpColumnFamily(RocksDbManager manager, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = manager.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private sealed class BundleChainView : IChainView
        {
            private readonly RocksDbChainStoreBundle _bundle;

            public BundleChainView(RocksDbChainStoreBundle bundle)
            {
                _bundle = bundle;
            }

            public long HeadNumber => FreezeBoundary;

            public byte[] BlockId(long number) => _bundle.Blocks.GetHashByNumberAsync(number).GetAwaiter().GetResult();

            public BlockHeader Header(long number) => _bundle.Blocks.GetByNumberAsync(number).GetAwaiter().GetResult();

            public IReadOnlyList<ReceiptForStorage> Receipts(long number)
            {
                var receipts = _bundle.Receipts.GetByBlockNumberAsync(number).GetAwaiter().GetResult();
                return receipts
                    .Select(r => new ReceiptForStorage(r.PostStateOrStatus, r.CumulativeGasUsed, r.Logs))
                    .ToList();
            }
        }

        private sealed class EmptyHistoricalLogScan : IHistoricalLogScan
        {
            public Task<IReadOnlyList<ResolvedLog>> ScanAsync(LogFilter filter, long fromBlock, long toBlock)
                => Task.FromResult<IReadOnlyList<ResolvedLog>>(new List<ResolvedLog>());
        }

        private sealed class FixedFinality : IFinalitySource
        {
            public long FinalizedBlockNumber { get; set; }
        }

        private sealed class SyntheticChainView : IChainView
        {
            private readonly Dictionary<long, List<ReceiptForStorage>> _receipts = new Dictionary<long, List<ReceiptForStorage>>();
            public long HeadNumber { get; private set; }

            public void SetBlock(long number, params Log[] logs)
            {
                var receipt = new ReceiptForStorage(new byte[] { 0x01 }, 21000, logs);
                _receipts[number] = new List<ReceiptForStorage> { receipt };
                if (number > HeadNumber) HeadNumber = number;
            }

            public byte[] BlockId(long number) => BitConverter.GetBytes(number);

            public BlockHeader Header(long number) => null;

            public IReadOnlyList<ReceiptForStorage> Receipts(long number) =>
                _receipts.TryGetValue(number, out var r) ? r : new List<ReceiptForStorage>();
        }
    }
}
