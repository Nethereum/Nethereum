using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Freezer;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Freezer;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerFilterMapsDecodeOnceTests : IDisposable
    {
        private static readonly FilterMapsParams TinyParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 1,
            logValuesPerMap: 3,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        private const int BlockCount = 40;
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"fmdecodeonce_{Guid.NewGuid():N}");

        public void Dispose()
        {
            foreach (var manager in _managers) { try { manager.Dispose(); } catch { } }
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public void Given_MultiEpochFixture_When_BulkRenderRuns_Then_EachCompletedEpochBlockIsDecodedExactlyOnce()
        {
            using var h = OpenRolledStore(BlockCount, maxFileSize: 0);
            var counting = new CountingFrozenReadSource(h.Freezer);

            var (fmStore, fmIndexer, progress) = WireIndexer(h, counting);
            var builder = new FreezerBulkIndexBuilder(counting, h.Codecs, ingestor: null, fmStore, fmIndexer, progress);

            var epochs = builder.RenderFilterMaps(headOverride: counting.Items);

            Assert.True(epochs > 1, "fixture must span multiple complete epochs");

            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            Assert.True(range.BlocksAfterLast > 0 && range.BlocksAfterLast < BlockCount,
                "expected a genuine completed-epoch region with an uncommitted tail remaining");

            for (var b = 0L; b < range.BlocksAfterLast; b++)
            {
                Assert.True(counting.ReceiptsDecodeCounts.TryGetValue(b, out var count),
                    $"completed-epoch block {b} was never decoded");
                Assert.Equal(1, count);
            }

            foreach (var kv in counting.ReceiptsDecodeCounts)
                Assert.True(kv.Value <= 2, $"block {kv.Key} decoded {kv.Value} times -- more than the tolerated sub-epoch tail slack");
        }

        [Fact]
        public void Given_FixtureWithAnUnsealedReceiptsTail_When_BulkRenderRuns_Then_TheOpenTailIsChunkReadNotPerItem()
        {
            const int tinyMaxFileSize = 700;
            using var h = OpenRolledStore(BlockCount, maxFileSize: tinyMaxFileSize);

            var sealedHead = h.Freezer.SealedHead("receipts");
            Assert.True(sealedHead > 0, "need several sealed files to exercise the boundary");
            Assert.True(sealedHead < h.Freezer.Items, "need an open (unsealed) tail file for this fixture to be meaningful");

            var counting = new CountingFrozenReadSource(h.Freezer);
            var (fmStore, fmIndexer, progress) = WireIndexer(h, counting);
            var builder = new FreezerBulkIndexBuilder(counting, h.Codecs, ingestor: null, fmStore, fmIndexer, progress);

            builder.RenderFilterMaps(headOverride: counting.Items);

            Assert.Equal(0, counting.ReadReceiptsSingleItemCalls);
            Assert.True(counting.DecodeSealedChunkCalls > 0, "expected the render to read receipts via the chunk primitive");

            var range = fmStore.ReadRange();
            Assert.NotNull(range);
            Assert.True(range.BlocksAfterLast > sealedHead,
                "expected the render to have progressed past the sealed boundary into the open tail");
        }

        [Fact]
        public void Given_AnUncommittedTailBeyondTheLastEpoch_When_QueryingProgress_Then_LogRenderProgressBlockIsLiveWhileLogIndexRenderedHeadIsDurable()
        {
            using var h = OpenRolledStore(BlockCount, maxFileSize: 0);
            var counting = new CountingFrozenReadSource(h.Freezer);

            var (fmStore, fmIndexer, progress) = WireIndexer(h, counting);
            var builder = new FreezerBulkIndexBuilder(counting, h.Codecs, ingestor: null, fmStore, fmIndexer, progress);

            builder.RenderFilterMaps(headOverride: counting.Items);

            var durableHead = builder.LogIndexRenderedHead;
            var liveHead = builder.LogRenderProgressBlock;

            Assert.True(durableHead < BlockCount, "fixture must leave an uncommitted tail for this test to be meaningful");
            Assert.Equal(fmIndexer.LogValueCountedThroughBlock, liveHead);
            Assert.True(liveHead > durableHead,
                $"expected live render progress ({liveHead}) ahead of the durable epoch-committed head ({durableHead})");
        }

        private (RocksDbFilterMapsStore fmStore, FilterMapsIndexer fmIndexer, FreezerHistoryIndexProgress progress) WireIndexer(
            Harness h, IFrozenReadSource freezerForWiring)
        {
            var indexDir = Path.Combine(_root, "index_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(indexDir);
            var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = indexDir }, CatalogueScope.FreezerHistory);
            _managers.Add(indexRocks);

            var fmStore = new RocksDbFilterMapsStore(indexRocks);
            var chainView = new SegmentChainView(freezerForWiring, h.Codecs);
            var finality = new FreezerHeadFinalitySource(freezerForWiring);
            var fmIndexer = new FilterMapsIndexer(fmStore, chainView, finality, TinyParams);
            var progress = new FreezerHistoryIndexProgress(indexRocks, HistoryColumnFamilies.Control);
            return (fmStore, fmIndexer, progress);
        }

        private readonly List<RocksDbManager> _managers = new List<RocksDbManager>();

        private sealed class Harness : IDisposable
        {
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }
            private readonly string _tempDir;

            public Harness(FreezerCore freezer, FreezerCodecSet codecs, string tempDir)
            {
                Freezer = freezer;
                Codecs = codecs;
                _tempDir = tempDir;
            }

            public void Dispose()
            {
                Freezer.Dispose();
                try { Directory.Delete(_tempDir, recursive: true); } catch { }
            }
        }

        private Harness OpenRolledStore(int blockCount, int maxFileSize)
        {
            var tempDir = Path.Combine(_root, "fz_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var codecs = new FreezerCodecSet();
            var body = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null);
            var bal = Array.Empty<byte>();

            var layout = maxFileSize > 0 ? new FreezerLayout(tempDir, maxFileSize: maxFileSize) : new FreezerLayout(tempDir);

            using (var writer = FreezerCore.Open(layout, FreezerOpenMode.Append))
            {
                var batch = writer.BeginBatch();
                for (long b = 0; b < blockCount; b++)
                {
                    var hash = new byte[32];
                    hash[0] = (byte)b;
                    batch.AppendCluster(b, new FrozenBlockCluster(
                        codecs.Headers.Encode(SyntheticHeader(100 + b)), hash,
                        codecs.Bodies.Encode(body), codecs.Receipts.Encode(ReceiptsFor(b)), codecs.Bals.Encode(bal)));
                }
                batch.Commit();
            }

            var freezer = FreezerCore.Open(layout, FreezerOpenMode.ReadOnly);
            return new Harness(freezer, codecs, tempDir);
        }

        private static List<ReceiptForStorage> ReceiptsFor(long b)
        {
            var address = new byte[20];
            address[19] = (byte)(b + 1);
            var logs = new List<Log>
            {
                new Log { Address = address.ToHex(true), Topics = new List<byte[]> { TopicBytes((byte)(b + 1)) }, Data = new byte[] { (byte)b } }
            };
            return new List<ReceiptForStorage> { new ReceiptForStorage(new byte[] { 0x01 }, 21000 * (b + 1), logs) };
        }

        private static byte[] TopicBytes(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static BlockHeader SyntheticHeader(long blockNumber) => new()
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            BlockNumber = new EvmUInt256((ulong)blockNumber),
            LogsBloom = new byte[256],
            Difficulty = EvmUInt256.Zero,
            Timestamp = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
        };

        private sealed class CountingFrozenReadSource : IFrozenReadSource
        {
            private readonly IFrozenReadSource _inner;
            private readonly object _gate = new object();

            public readonly Dictionary<long, int> ReceiptsDecodeCounts = new Dictionary<long, int>();
            public int ReadReceiptsSingleItemCalls;
            public int DecodeSealedChunkCalls;

            public CountingFrozenReadSource(IFrozenReadSource inner) => _inner = inner;

            public long Items => _inner.Items;

            public long CommittedHead(params string[] tableNames) => _inner.CommittedHead(tableNames);

            public byte[] ReadHeader(long blockNumber) => _inner.ReadHeader(blockNumber);

            public byte[] ReadHash(long blockNumber) => _inner.ReadHash(blockNumber);

            public byte[] ReadBody(long blockNumber) => _inner.ReadBody(blockNumber);

            public byte[] ReadReceipts(long blockNumber)
            {
                lock (_gate)
                {
                    ReadReceiptsSingleItemCalls++;
                    Track(blockNumber);
                }
                return _inner.ReadReceipts(blockNumber);
            }

            public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
                string tableName, long startBlock, int maxItems, int? maxDegreeOfParallelism = null)
            {
                var result = _inner.DecodeSealedRange(tableName, startBlock, maxItems, maxDegreeOfParallelism);
                if (tableName == "receipts") TrackAll(result);
                return result;
            }

            public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
                string tableName, long startBlock, int maxItems, long endExclusiveBlock, int? maxDegreeOfParallelism = null)
            {
                var result = _inner.DecodeSealedRange(tableName, startBlock, maxItems, endExclusiveBlock, maxDegreeOfParallelism);
                if (tableName == "receipts") TrackAll(result);
                return result;
            }

            public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedChunk(
                string tableName, long startBlock, int targetBytes, long endExclusiveBlock, int? maxDegreeOfParallelism = null)
            {
                var result = _inner.DecodeSealedChunk(tableName, startBlock, targetBytes, endExclusiveBlock, maxDegreeOfParallelism);
                if (tableName == "receipts")
                {
                    lock (_gate) DecodeSealedChunkCalls++;
                    TrackAll(result);
                }
                return result;
            }

            private void TrackAll(IReadOnlyList<(long BlockNumber, byte[] Decoded)> items)
            {
                lock (_gate)
                {
                    foreach (var item in items)
                        Track(item.BlockNumber);
                }
            }

            private void Track(long blockNumber) =>
                ReceiptsDecodeCounts[blockNumber] = ReceiptsDecodeCounts.TryGetValue(blockNumber, out var c) ? c + 1 : 1;
        }
    }
}
