using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.History
{
    public sealed class SyncBulkSaveService : IDisposable
    {
        private static readonly byte[] ProgressKey = Encoding.UTF8.GetBytes("backfill_progress");

        public const int DefaultCheckpointIntervalBlocks = 4096;

        public const int DefaultCheckpointIntervalSeconds = 30;

        public const int DefaultCheckpointMaxPendingEntries = 2_000_000;

        private readonly RocksDb _db;
        private readonly ColumnFamilyHandle _txBody, _receiptBody, _blockHeader, _blockMeta, _control;
        private readonly Dictionary<string, ColumnFamilyHandle> _cfByName = new();
        private readonly WriteOptions _walOff = new WriteOptions().DisableWal(1);
        private readonly WriteOptions _durable = new WriteOptions().SetSync(true);
        private readonly BulkIndexIngestor _indexIngestor;

        private readonly object _writerGate = new object();
        private readonly HashSet<string> _pendingSeqCfs = new();

        public SyncBulkSaveService(
            RocksDb db, string scratchDir,
            int checkpointIntervalBlocks = DefaultCheckpointIntervalBlocks,
            int checkpointIntervalSeconds = DefaultCheckpointIntervalSeconds,
            int maxPendingEntries = DefaultCheckpointMaxPendingEntries)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            if (scratchDir == null) throw new ArgumentNullException(nameof(scratchDir));
            _txBody = db.GetColumnFamily(HistoryColumnFamilies.TxBody);
            _receiptBody = db.GetColumnFamily(HistoryColumnFamilies.ReceiptBody);
            _blockHeader = db.GetColumnFamily(HistoryColumnFamilies.BlockHeader);
            _blockMeta = db.GetColumnFamily(HistoryColumnFamilies.BlockMeta);
            _control = db.GetColumnFamily(HistoryColumnFamilies.Control);

            _indexIngestor = new BulkIndexIngestor(
                db, scratchDir,
                new[] { HistoryColumnFamilies.TxHashIndex, HistoryColumnFamilies.BlockHashIndex },
                windowHead => _db.Put(ProgressKey, HistoryKeys.BlockKey(windowHead), _control, _durable),
                checkpointIntervalBlocks, checkpointIntervalSeconds, maxPendingEntries, sortDegreeOfParallelism: 1);
        }

        public ulong? LastCompletedBlock()
        {
            var raw = _db.Get(ProgressKey, _control);
            return raw == null ? (ulong?)null : HistoryKeys.ReadBlockNumber(raw);
        }

        public void PrepareResume() { }

        public void WriteChunk(IReadOnlyList<HistoryBlockWrite> blocks)
        {
            if (blocks == null || blocks.Count == 0) return;
            lock (_writerGate)
            {
            WriteChunkCore(blocks);
            }
        }

        private void WriteChunkCore(IReadOnlyList<HistoryBlockWrite> blocks)
        {

            using (var batch = new WriteBatch())
            {
                foreach (var b in blocks)
                {
                    var blockKey = HistoryKeys.BlockKey(b.BlockNumber);
                    if (b.Header != null) batch.Put(blockKey, b.Header, _blockHeader);
                    if (b.Meta != null) batch.Put(blockKey, b.Meta, _blockMeta);
                    var txs = b.Transactions;
                    for (int i = 0; i < txs.Count; i++)
                    {
                        var txKey = HistoryKeys.TxKey(b.BlockNumber, (uint)i);
                        if (txs[i].Tx != null) batch.Put(txKey, txs[i].Tx, _txBody);
                        if (txs[i].Receipt != null) batch.Put(txKey, txs[i].Receipt, _receiptBody);
                    }
                    if (b.SequentialExtra != null)
                        foreach (var w in b.SequentialExtra) { batch.Put(w.Key, w.Value, Cf(w.Cf)); _pendingSeqCfs.Add(w.Cf); }
                }
                _db.Write(batch, _walOff);
            }

            var entries = new List<(string Cf, byte[] Key, byte[] Value)>();
            foreach (var b in blocks)
            {
                if (b.BlockHash != null)
                    entries.Add((HistoryColumnFamilies.BlockHashIndex, b.BlockHash, HistoryKeys.BlockKey(b.BlockNumber)));
                var txs = b.Transactions;
                for (int i = 0; i < txs.Count; i++)
                    if (txs[i].TxHash != null)
                        entries.Add((HistoryColumnFamilies.TxHashIndex, txs[i].TxHash, HistoryKeys.TxKey(b.BlockNumber, (uint)i)));
                if (b.IndexExtra != null)
                    foreach (var w in b.IndexExtra) entries.Add(w);
            }

            if (_indexIngestor.Accumulate(blocks.Count, blocks[blocks.Count - 1].BlockNumber, entries))
                CheckpointCore();
        }

        public void Checkpoint()
        {
            lock (_writerGate)
            {
            CheckpointCore();
            }
        }

        private void CheckpointCore()
        {
            if (_indexIngestor.HasPendingWindow)
            {
                FlushBodies(_pendingSeqCfs);
                _pendingSeqCfs.Clear();
            }
            _indexIngestor.Checkpoint();
        }

        public void Finish()
        {
            lock (_writerGate)
            {
                CheckpointCore();
                _indexIngestor.Finish();
                foreach (var cf in new[] { _blockHeader, _blockMeta, _txBody, _receiptBody })
                    _db.CompactRange((byte[])null, (byte[])null, cf);
            }
        }

        private void FlushBodies(HashSet<string> extraSequentialCfs = null)
        {
            var fo = new FlushOptions();
            foreach (var cf in new[] { _blockHeader, _blockMeta, _txBody, _receiptBody })
                Native.Instance.rocksdb_flush_cf(_db.Handle, fo.Handle, cf.Handle);
            if (extraSequentialCfs != null)
                foreach (var name in extraSequentialCfs)
                    Native.Instance.rocksdb_flush_cf(_db.Handle, fo.Handle, Cf(name).Handle);
        }

        private ColumnFamilyHandle Cf(string name)
            => _cfByName.TryGetValue(name, out var h) ? h : (_cfByName[name] = _db.GetColumnFamily(name));

        public void Dispose() { }
    }
}
