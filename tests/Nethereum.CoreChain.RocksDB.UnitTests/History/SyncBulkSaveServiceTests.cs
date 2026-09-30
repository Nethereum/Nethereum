using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class BulkLoadSessionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bulk_{Guid.NewGuid():N}");
        private readonly RocksDb _db;
        private readonly RocksDbHistoryStore _store;

        public BulkLoadSessionTests()
        {
            Directory.CreateDirectory(_dir);
            var cache = RocksProfiles.CreateSharedCache(32 * 1024 * 1024);
            _db = RocksDb.Open(RocksProfiles.Db(2, 64 * 1024 * 1024), Path.Combine(_dir, "db"), RocksProfiles.BuildColumnFamilies(cache));
            _store = new RocksDbHistoryStore(_db);
        }

        public void Dispose()
        {
            _db.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public void WriteChunk_Bodies_And_SstIngestedHashIndex_AreReadable()
        {
            using var session = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch"));
            session.WriteChunk(new[] { Block(1, 2), Block(2, 2) });

            Assert.Equal("hdr1", Str(_store.GetHeader(1)));
            Assert.Equal("tx2.1", Str(_store.GetTransaction(2, 1)));
            Assert.Equal("rcpt2.0", Str(_store.GetReceipt(2, 0)));
            Assert.Equal(new TxLocation(2, 1), _store.FindTransaction(TxHash(2, 1)));
            Assert.Equal(2UL, _store.FindBlock(BlockHash(2)));
            Assert.Equal(2UL, session.LastCompletedBlock());
        }

        [Fact]
        public void PrepareResume_NoBulkCheckpoint_DoesNotWipeWalkerHeaders()
        {
            _db.Put(HistoryKeys.BlockKey(5_000_000), Enc("hdr5m"), _db.GetColumnFamily(HistoryColumnFamilies.BlockHeader));
            using var session = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch"));
            Assert.Null(session.LastCompletedBlock());
            session.PrepareResume();
            Assert.Equal("hdr5m", Str(_store.GetHeader(5_000_000)));
        }

        [Fact]
        public void Crash_MidChunk_Resume_PartialTailSurvives_ThenReWrittenFromCheckpoint()
        {
            using (var session = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch1")))
                session.WriteChunk(new[] { Block(1, 1), Block(2, 1), Block(3, 1) });

            WritePartialBodies(4);
            WritePartialBodies(5);
            Assert.NotNull(_store.GetHeader(4));

            using var resumed = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch2"));
            Assert.Equal(3UL, resumed.LastCompletedBlock());
            resumed.PrepareResume();

            Assert.NotNull(_store.GetHeader(4));
            Assert.NotNull(_store.GetHeader(3));

            resumed.WriteChunk(new[] { Block(4, 1), Block(5, 1) });
            Assert.Equal("hdr4", Str(_store.GetHeader(4)));
            Assert.Equal(new TxLocation(4, 0), _store.FindTransaction(TxHash(4, 0)));
            Assert.Equal(5UL, resumed.LastCompletedBlock());
        }

        [Fact]
        public void WriteChunk_MakesBodiesDurable_BeforeAdvancingCheckpoint()
        {
            using var session = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch"));
            session.WriteChunk(new[] { Block(1, 2), Block(2, 2) });

            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.TxBody));
            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.ReceiptBody));
            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.BlockHeader));
            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.BlockMeta));
        }

        [Fact]
        public void Resume_IsNonDestructive_ThenReWritesPartialChunkFromCheckpoint()
        {
            using (var session = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch1")))
                session.WriteChunk(new[] { Block(1, 1), Block(2, 1) });

            WriteDurableBodyAndIndex(3);
            _db.Put(HistoryKeys.BlockKey(9_000_000), Enc("hdr9m"), _db.GetColumnFamily(HistoryColumnFamilies.BlockHeader));

            using var resumed = new SyncBulkSaveService(_db, Path.Combine(_dir, "scratch2"));
            Assert.Equal(2UL, resumed.LastCompletedBlock());
            resumed.PrepareResume();

            Assert.NotNull(_store.GetTransaction(3, 0));
            Assert.Equal("hdr9m", Str(_store.GetHeader(9_000_000)));

            resumed.WriteChunk(new[] { Block(3, 1) });
            Assert.Equal("tx3.0", Str(_store.GetTransaction(3, 0)));
            Assert.Equal(new TxLocation(3, 0), _store.FindTransaction(TxHash(3, 0)));
            Assert.Equal(3UL, _store.FindBlock(BlockHash(3)));
            Assert.Equal(3UL, resumed.LastCompletedBlock());
        }

        [Fact]
        public void Checkpoint_IsDeferred_UntilBlockThreshold()
        {
            using var session = new SyncBulkSaveService(
                _db, Path.Combine(_dir, "scratch"), checkpointIntervalBlocks: 4, checkpointIntervalSeconds: 0);

            session.WriteChunk(new[] { Block(1, 1) });
            Assert.Equal(1UL, session.LastCompletedBlock());

            session.WriteChunk(new[] { Block(2, 1), Block(3, 1) });
            Assert.Equal(1UL, session.LastCompletedBlock());
            Assert.Equal("tx3.0", Str(_store.GetTransaction(3, 0)));
            Assert.Null(_store.FindTransaction(TxHash(3, 0)));

            session.WriteChunk(new[] { Block(4, 1), Block(5, 1) });
            Assert.Equal(5UL, session.LastCompletedBlock());
            Assert.Equal(new TxLocation(3, 0), _store.FindTransaction(TxHash(3, 0)));
            Assert.Equal(new TxLocation(5, 0), _store.FindTransaction(TxHash(5, 0)));
            Assert.Equal(5UL, _store.FindBlock(BlockHash(5)));
            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.TxBody));
            Assert.Equal(0, ActiveMemtableEntries(HistoryColumnFamilies.ReceiptBody));
        }

        [Fact]
        public void Checkpoint_IsGovernedBy_MaxPendingEntries_WhenBlockIntervalHigh_AndTimerOff()
        {
            using var session = new SyncBulkSaveService(
                _db, Path.Combine(_dir, "scratch"),
                checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0, maxPendingEntries: 4);

            session.WriteChunk(new[] { Block(1, 1) });
            Assert.Equal(1UL, session.LastCompletedBlock());

            session.WriteChunk(new[] { Block(2, 1) });
            Assert.Equal(1UL, session.LastCompletedBlock());
            Assert.Null(_store.FindTransaction(TxHash(2, 0)));

            session.WriteChunk(new[] { Block(3, 1) });
            Assert.Equal(3UL, session.LastCompletedBlock());
            Assert.Equal(new TxLocation(2, 0), _store.FindTransaction(TxHash(2, 0)));
            Assert.Equal(new TxLocation(3, 0), _store.FindTransaction(TxHash(3, 0)));
        }

        [Fact]
        public void Finish_ForcesFinalCheckpoint_ForPendingTail()
        {
            using var session = new SyncBulkSaveService(
                _db, Path.Combine(_dir, "scratch"), checkpointIntervalBlocks: 1000, checkpointIntervalSeconds: 0);
            session.WriteChunk(new[] { Block(1, 1) });
            session.WriteChunk(new[] { Block(2, 1) });
            Assert.Equal(1UL, session.LastCompletedBlock());

            session.Finish();

            Assert.Equal(2UL, session.LastCompletedBlock());
            Assert.Equal(new TxLocation(2, 0), _store.FindTransaction(TxHash(2, 0)));
        }

        private long ActiveMemtableEntries(string cf) =>
            long.Parse(_db.GetProperty("rocksdb.num-entries-active-mem-table", _db.GetColumnFamily(cf)));

        private void WriteDurableBodyAndIndex(ulong n)
        {
            using var b = new WriteBatch();
            b.Put(HistoryKeys.BlockKey(n), Enc($"hdr{n}"), _db.GetColumnFamily(HistoryColumnFamilies.BlockHeader));
            b.Put(HistoryKeys.BlockKey(n), Enc($"meta{n}"), _db.GetColumnFamily(HistoryColumnFamilies.BlockMeta));
            b.Put(HistoryKeys.TxKey(n, 0), Enc($"tx{n}.0"), _db.GetColumnFamily(HistoryColumnFamilies.TxBody));
            b.Put(HistoryKeys.TxKey(n, 0), Enc($"rcpt{n}.0"), _db.GetColumnFamily(HistoryColumnFamilies.ReceiptBody));
            b.Put(BlockHash(n), HistoryKeys.BlockKey(n), _db.GetColumnFamily(HistoryColumnFamilies.BlockHashIndex));
            b.Put(TxHash(n, 0), HistoryKeys.TxKey(n, 0), _db.GetColumnFamily(HistoryColumnFamilies.TxHashIndex));
            _db.Write(b);
        }

        private void WritePartialBodies(ulong n)
        {
            using var b = new WriteBatch();
            b.Put(HistoryKeys.BlockKey(n), Enc($"hdr{n}"), _db.GetColumnFamily(HistoryColumnFamilies.BlockHeader));
            b.Put(HistoryKeys.TxKey(n, 0), Enc($"tx{n}.0"), _db.GetColumnFamily(HistoryColumnFamilies.TxBody));
            _db.Write(b);
        }

        private static HistoryBlockWrite Block(ulong n, int txCount)
        {
            var txs = new List<HistoryTxWrite>();
            for (uint i = 0; i < txCount; i++)
                txs.Add(new HistoryTxWrite { TxHash = TxHash(n, i), Tx = Enc($"tx{n}.{i}"), Receipt = Enc($"rcpt{n}.{i}") });
            return new HistoryBlockWrite { BlockNumber = n, BlockHash = BlockHash(n), Header = Enc($"hdr{n}"), Meta = Enc($"meta{n}"), Transactions = txs };
        }

        private static byte[] TxHash(ulong n, uint i) { var h = new byte[32]; h[0] = (byte)(0x10 + n); h[1] = (byte)i; return h; }
        private static byte[] BlockHash(ulong n) { var h = new byte[32]; h[0] = (byte)(0xB0 + n); return h; }
        private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);
        private static string Str(byte[] b) => b == null ? null : Encoding.UTF8.GetString(b);
    }
}
