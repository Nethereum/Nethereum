using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class BulkIndexIngestorTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bulkidx_{Guid.NewGuid():N}");
        private int _scratchCounter;

        public BulkIndexIngestorTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private string NewScratchDir() => Path.Combine(_dir, "scratch_" + Interlocked.Increment(ref _scratchCounter));

        private RocksDb OpenDb(string name)
        {
            var cache = RocksProfiles.CreateSharedCache(16 * 1024 * 1024);
            return RocksDb.Open(RocksProfiles.Db(2, 32 * 1024 * 1024), Path.Combine(_dir, name), RocksProfiles.BuildColumnFamilies(cache));
        }

        [Fact]
        public void Given_AccumulateBelowThreshold_Then_NoIngestAndCursorUnchanged_TwinAtThreshold_IngestsAndAdvancesCursor()
        {
            using var db = OpenDb("boundary");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 3, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000);

            var crossed1 = ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) });
            Assert.True(crossed1);
            ingestor.Checkpoint();
            Assert.Equal(new ulong[] { 1 }, cursorWrites);
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(1)));

            var crossed2 = ingestor.Accumulate(1, 2, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(2), HistoryKeys.BlockKey(2)) });
            Assert.False(crossed2);
            Assert.Null(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(2)));
            Assert.Single(cursorWrites);

            var crossed3 = ingestor.Accumulate(2, 4, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(4), HistoryKeys.BlockKey(4)) });
            Assert.True(crossed3);
            ingestor.Checkpoint();
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(2)));
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(4)));
            Assert.Equal(new ulong[] { 1, 4 }, cursorWrites);
        }

        [Fact]
        public void Given_MaxPendingEntries_When_Crossed_Then_ChekpointsRegardlessOfBlockInterval()
        {
            using var db = OpenDb("entrycap");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 3);

            Assert.True(ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) }));
            ingestor.Checkpoint();

            Assert.False(ingestor.Accumulate(1, 2, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(2), HistoryKeys.BlockKey(2)) }));

            var entries = new[]
            {
                (HistoryColumnFamilies.BlockHashIndex, BlockHash(3), HistoryKeys.BlockKey(3)),
                (HistoryColumnFamilies.BlockHashIndex, BlockHash(4), HistoryKeys.BlockKey(4)),
            };
            Assert.True(ingestor.Accumulate(2, 4, entries));
            ingestor.Checkpoint();
            Assert.Equal(new ulong[] { 1, 4 }, cursorWrites);
        }

        [Fact]
        public void Given_PendingBytesBelowTwoGiB_Then_NoFlush_TwinAtTwoGiB_FlushesAndAdvancesCursor()
        {
            using var db = OpenDb("bytethreshold");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                maxPendingBytes: 100, forceCheckpointOnFirstAccumulate: false);

            var crossed1 = ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) });
            Assert.False(crossed1);
            Assert.Empty(cursorWrites);

            var crossed2 = ingestor.Accumulate(1, 2, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(2), HistoryKeys.BlockKey(2)) });
            Assert.False(crossed2);
            Assert.Empty(cursorWrites);

            var crossed3 = ingestor.Accumulate(1, 3, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(3), HistoryKeys.BlockKey(3)) });
            Assert.True(crossed3);
            ingestor.Checkpoint();
            Assert.Equal(new ulong[] { 3 }, cursorWrites);
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(1)));
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(3)));
        }

        [Fact]
        public void Given_ResidentBytesExceedConfiguredCeiling_Then_ThrowsInsteadOfFlushingUndersizedRun()
        {
            using var db = OpenDb("residentceiling");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                maxResidentBytes: 100, forceCheckpointOnFirstAccumulate: false);

            Assert.Throws<InvalidOperationException>(() =>
                ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) }));

            Assert.Empty(cursorWrites);
        }

        [Fact]
        public void Given_ForceCheckpointOnFirstAccumulateFalse_When_FirstAccumulateBelowByteThreshold_Then_NoFlush()
        {
            using var db = OpenDb("optout_first");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                maxPendingBytes: long.MaxValue, forceCheckpointOnFirstAccumulate: false);

            var crossed = ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) });

            Assert.False(crossed);
            Assert.Empty(cursorWrites);
            Assert.Null(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(1)));
        }

        [Fact]
        public void Given_TheSameChunks_When_DrivenThroughSyncBulkSaveServiceAndBulkIndexIngestor_Then_IngestedRowsAreByteIdentical()
        {
            using var dbA = OpenDb("via_service");
            using var dbB = OpenDb("via_ingestor");

            using var service = new SyncBulkSaveService(dbA, NewScratchDir());
            var ingestor = new BulkIndexIngestor(
                dbB, NewScratchDir(), new[] { HistoryColumnFamilies.TxHashIndex, HistoryColumnFamilies.BlockHashIndex },
                _ => { }, checkpointIntervalBlocks: 4096, checkpointIntervalSeconds: 0);

            var blocks = new List<HistoryBlockWrite> { Block(1, 2), Block(2, 3), Block(3, 1) };
            service.WriteChunk(blocks);
            service.Finish();

            foreach (var b in blocks)
            {
                var entries = new List<(string Cf, byte[] Key, byte[] Value)>
                {
                    (HistoryColumnFamilies.BlockHashIndex, b.BlockHash, HistoryKeys.BlockKey(b.BlockNumber)),
                };
                for (var i = 0; i < b.Transactions.Count; i++)
                    entries.Add((HistoryColumnFamilies.TxHashIndex, b.Transactions[i].TxHash, HistoryKeys.TxKey(b.BlockNumber, (uint)i)));
                ingestor.Accumulate(1, b.BlockNumber, entries);
            }
            ingestor.Finish();

            Assert.Equal(AllRows(dbA, HistoryColumnFamilies.BlockHashIndex), AllRows(dbB, HistoryColumnFamilies.BlockHashIndex));
            Assert.Equal(AllRows(dbA, HistoryColumnFamilies.TxHashIndex), AllRows(dbB, HistoryColumnFamilies.TxHashIndex));
            Assert.NotEmpty(AllRows(dbA, HistoryColumnFamilies.TxHashIndex));
        }

        [Fact]
        public void Given_TheGatedConcurrentWorkload_Then_NoEntriesAreLostAndTheCursorIsAContiguousWatermark()
        {
            using var db = OpenDb("concurrent");
            var reportedCursors = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => { lock (reportedCursors) reportedCursors.Add(w); },
                checkpointIntervalBlocks: 40, checkpointIntervalSeconds: 0, maxPendingEntries: 100);

            const int perThread = 800;
            void Drive(int threadTag)
            {
                for (var i = 0; i < perThread; i++)
                {
                    var n = (ulong)(threadTag * perThread + i);
                    var entries = new[] { (HistoryColumnFamilies.BlockHashIndex, ConcurrentKey(threadTag, i), HistoryKeys.BlockKey(n)) };
                    if (ingestor.Accumulate(1, n, entries)) ingestor.Checkpoint();
                }
            }

            Task.WaitAll(Task.Run(() => Drive(0)), Task.Run(() => Drive(1)));
            ingestor.Finish();

            Assert.Equal(perThread * 2, CountRows(db, HistoryColumnFamilies.BlockHashIndex));

            Assert.NotEmpty(reportedCursors);
            for (var i = 1; i < reportedCursors.Count; i++)
                Assert.True(reportedCursors[i] >= reportedCursors[i - 1], "cursor must never rewind");
            Assert.Equal((ulong)(perThread * 2 - 1), reportedCursors[reportedCursors.Count - 1]);
        }

        [Fact]
        public void Given_ALowerConcurrentWindowCommitsAfterAHigherOne_Then_TheUnconditionalCursorNeverRewinds()
        {
            using var db = OpenDb("noguard_needed");
            ulong cursor = 0;
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                windowHead => cursor = windowHead + 1,
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0);

            ingestor.Accumulate(1, 200, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(200), HistoryKeys.BlockKey(200)) });
            ingestor.Checkpoint();
            Assert.Equal(201UL, cursor);

            ingestor.Accumulate(1, 100, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(100), HistoryKeys.BlockKey(100)) });
            ingestor.Checkpoint();
            Assert.Equal(201UL, cursor);
        }

        [Fact]
        public void Given_AWindowThatLeavesAGapAboveTheFrontier_Then_TheCursorHoldsUntilTheGapFills_TwinOldMaxGuardOverClaims()
        {
            using var db = OpenDb("gap_frontier");
            ulong cursor = 0;
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                windowHead => cursor = windowHead + 1,
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0);

            ingestor.Accumulate(100, 99, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(99), HistoryKeys.BlockKey(99)) });
            ingestor.Checkpoint();
            Assert.Equal(100UL, cursor);

            ingestor.Accumulate(51, 250, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(250), HistoryKeys.BlockKey(250)) });
            ingestor.Checkpoint();
            Assert.Equal(100UL, cursor);

            ulong oldStyleCursor = 0;
            void OldStyleMaxGuardedAdvance(ulong candidate) { if (candidate > oldStyleCursor) oldStyleCursor = candidate; }
            OldStyleMaxGuardedAdvance(100);
            OldStyleMaxGuardedAdvance(251);
            Assert.Equal(251UL, oldStyleCursor);

            ingestor.Accumulate(100, 199, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(199), HistoryKeys.BlockKey(199)) });
            ingestor.Checkpoint();
            Assert.Equal(251UL, cursor);
        }

        [Fact]
        public void Given_FailureBetweenTwoCfIngests_Then_CursorUnchanged_AndReplaySucceeds()
        {
            using var db = OpenDb("fault_between_cfs");
            var missingCf = "test_only_missing_cf_" + Guid.NewGuid().ToString("N");
            var cursorWrites = new List<ulong>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000);

            var entries = new[]
            {
                (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)),
                (missingCf, TxHash(1, 0), HistoryKeys.TxKey(1, 0)),
            };
            Assert.True(ingestor.Accumulate(1, 1, entries));

            Assert.ThrowsAny<Exception>(() => ingestor.Checkpoint());

            Assert.Empty(cursorWrites);

            db.CreateColumnFamily(RocksProfiles.IndexSstOptions(), missingCf);
            ingestor.Checkpoint();

            Assert.Equal(new ulong[] { 1 }, cursorWrites);
            Assert.NotNull(ReadRow(db, HistoryColumnFamilies.BlockHashIndex, BlockHash(1)));
            Assert.NotNull(ReadRow(db, missingCf, TxHash(1, 0)));

        }

        [Fact]
        public void Given_AControlledRun_When_Checkpointed_Then_StepLogCarriesRunSerializedBytesAndSstRunsCf()
        {
            using var db = OpenDb("metrics_fields");
            var loggedLines = new List<string>();
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                _ => { },
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                stepLog: loggedLines.Add);

            ingestor.Accumulate(1, 1, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(1), HistoryKeys.BlockKey(1)) });
            ingestor.Checkpoint();

            Assert.Single(loggedLines);
            Assert.Contains("run_serialized_bytes=40", loggedLines[0]);
            Assert.Contains("sst_runs_cf=1", loggedLines[0]);

            ingestor.Accumulate(1, 2, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(2), HistoryKeys.BlockKey(2)) });
            ingestor.Accumulate(1, 3, new[] { (HistoryColumnFamilies.BlockHashIndex, BlockHash(3), HistoryKeys.BlockKey(3)) });
            ingestor.Checkpoint();

            Assert.Equal(2, loggedLines.Count);
            Assert.Contains("run_serialized_bytes=80", loggedLines[1]);
            Assert.Contains("sst_runs_cf=2", loggedLines[1]);

            Assert.Contains("run cf=" + HistoryColumnFamilies.BlockHashIndex, loggedLines[1]);
            Assert.Contains("entries=2", loggedLines[1]);
            Assert.Contains("pending_compaction=", loggedLines[1]);
        }

        [Fact]
        public void Given_ARunWithManyRandomKeys_When_ParallelSorted_Then_SstKeysAreFullySortedAndMatchSingleThreadedOrder()
        {
            using var db = OpenDb("parallel_sort");
            var cursorWrites = new List<ulong>();
            const int n = 250_000;
            const int dop = 8;
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                sortDegreeOfParallelism: dop);

            var rnd = new Random(98765);
            var entries = new List<(string Cf, byte[] Key, byte[] Value)>(n);
            var referenceKeys = new List<byte[]>(n);
            for (var i = 0; i < n; i++)
            {
                var k = new byte[32];
                rnd.NextBytes(k);
                entries.Add((HistoryColumnFamilies.BlockHashIndex, k, HistoryKeys.BlockKey((ulong)i)));
                referenceKeys.Add(k);
            }

            Assert.True(n >= BulkIndexIngestor.ParallelSortThreshold);
            Assert.True(ingestor.Accumulate(1, 1, entries));
            ingestor.Checkpoint();

            var ingestedKeys = AllRowKeysHex(db, HistoryColumnFamilies.BlockHashIndex);
            Assert.Equal(n, ingestedKeys.Count);

            for (var i = 1; i < ingestedKeys.Count; i++)
                Assert.True(string.CompareOrdinal(ingestedKeys[i - 1], ingestedKeys[i]) < 0,
                    "SST keys must be in strict ascending order");

            var referenceHex = referenceKeys.ConvertAll(Convert.ToHexString);
            referenceHex.Sort(StringComparer.Ordinal);
            Assert.Equal(referenceHex, ingestedKeys);
        }

        [Fact]
        public void Given_ARunWithDuplicateKeys_When_ParallelSorted_Then_OneEntryPerKey()
        {
            using var db = OpenDb("parallel_sort_dup");
            var cursorWrites = new List<ulong>();
            const int distinctCount = 120_000;
            const int dop = 8;
            var ingestor = new BulkIndexIngestor(
                db, NewScratchDir(), new[] { HistoryColumnFamilies.BlockHashIndex },
                w => cursorWrites.Add(w),
                checkpointIntervalBlocks: 1, checkpointIntervalSeconds: 0, maxPendingEntries: 1_000_000,
                sortDegreeOfParallelism: dop);

            var rnd = new Random(12345);
            var keys = new byte[distinctCount][];
            for (var i = 0; i < distinctCount; i++)
            {
                var k = new byte[32];
                rnd.NextBytes(k);
                keys[i] = k;
            }

            var entries = new List<(string Cf, byte[] Key, byte[] Value)>(distinctCount * 2);
            for (var i = 0; i < distinctCount; i++)
                entries.Add((HistoryColumnFamilies.BlockHashIndex, keys[i], HistoryKeys.BlockKey((ulong)i)));
            for (var i = 0; i < distinctCount; i++)
                entries.Add((HistoryColumnFamilies.BlockHashIndex, keys[i], HistoryKeys.BlockKey((ulong)(distinctCount + i))));

            Assert.True(entries.Count >= BulkIndexIngestor.ParallelSortThreshold);
            Assert.True(ingestor.Accumulate(1, 1, entries));
            ingestor.Checkpoint();

            var ingestedKeys = AllRowKeysHex(db, HistoryColumnFamilies.BlockHashIndex);
            Assert.Equal(distinctCount, ingestedKeys.Count);
            for (var i = 1; i < ingestedKeys.Count; i++)
                Assert.True(string.CompareOrdinal(ingestedKeys[i - 1], ingestedKeys[i]) < 0,
                    "deduped keys must still be strictly ascending");
        }

        private static List<string> AllRowKeysHex(RocksDb db, string cf)
        {
            var result = new List<string>();
            using var it = db.NewIterator(db.GetColumnFamily(cf));
            for (it.SeekToFirst(); it.Valid(); it.Next())
                result.Add(Convert.ToHexString(it.Key()));
            return result;
        }

        private static byte[] ReadRow(RocksDb db, string cf, byte[] key) => db.Get(key, db.GetColumnFamily(cf));

        private static int CountRows(RocksDb db, string cf)
        {
            using var it = db.NewIterator(db.GetColumnFamily(cf));
            var n = 0;
            for (it.SeekToFirst(); it.Valid(); it.Next()) n++;
            return n;
        }

        private static List<KeyValuePair<string, string>> AllRows(RocksDb db, string cf)
        {
            var result = new List<KeyValuePair<string, string>>();
            using var it = db.NewIterator(db.GetColumnFamily(cf));
            for (it.SeekToFirst(); it.Valid(); it.Next())
                result.Add(new KeyValuePair<string, string>(Convert.ToHexString(it.Key()), Convert.ToHexString(it.Value())));
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        private static byte[] ConcurrentKey(int threadTag, int index)
        {
            var h = new byte[32];
            h[0] = (byte)(0x40 + threadTag);
            h[1] = (byte)(index >> 8);
            h[2] = (byte)index;
            return h;
        }

        private static byte[] BlockHash(ulong n) { var h = new byte[32]; h[0] = (byte)(0xB0 + n); return h; }
        private static byte[] TxHash(ulong n, uint i) { var h = new byte[32]; h[0] = (byte)(0x10 + n); h[1] = (byte)i; return h; }
        private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);

        private static HistoryBlockWrite Block(ulong n, int txCount)
        {
            var txs = new List<HistoryTxWrite>();
            for (uint i = 0; i < txCount; i++)
                txs.Add(new HistoryTxWrite { TxHash = TxHash(n, i), Tx = Enc($"tx{n}.{i}"), Receipt = Enc($"rcpt{n}.{i}") });
            return new HistoryBlockWrite { BlockNumber = n, BlockHash = BlockHash(n), Header = Enc($"hdr{n}"), Meta = Enc($"meta{n}"), Transactions = txs };
        }
    }
}
