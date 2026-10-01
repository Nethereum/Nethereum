using System.Collections.Generic;
using System.IO;
using System.Threading;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.Freezer.FilterMaps;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbFilterMapsStore : IFilterMapsStore
    {
        private static readonly WriteOptions WalOffOptions = new WriteOptions().DisableWal(1);
        private static readonly IngestExternalFileOptions DefaultIngest = new IngestExternalFileOptions();

        private const int FlushEveryEpochs = 4;

        private readonly RocksDbManager _manager;
        private readonly string _cf;
        private readonly ColumnFamilyHandle _cfHandle;

        private WriteBatch _bulkBatch;
        private int _bulkBatchOps;
        private readonly List<KeyValuePair<byte[], byte[]>> _pendingBaseRowGroups = new List<KeyValuePair<byte[], byte[]>>();
        private long _sstCounter;
        private int _epochsSinceFlush;

        private bool _dirty;

        public int ColumnFamilyFlushCount { get; private set; }

        public RocksDbFilterMapsStore(RocksDbManager manager, string columnFamily = null)
        {
            _manager = manager;
            _cf = columnFamily ?? HistoryColumnFamilies.LogFilterMaps;
            _cfHandle = _manager.GetColumnFamily(_cf);

            var scratchDir = Path.Combine(_manager.Options.DatabasePath, "fm-sst-scratch");
            if (Directory.Exists(scratchDir))
            {
                foreach (var stale in Directory.GetFiles(scratchDir, "*.sst"))
                {
                    try { File.Delete(stale); } catch { }
                }
            }
        }

        public void BeginBulk()
        {
            if (_bulkBatch != null) return;
            _bulkBatch = new WriteBatch();
            _bulkBatchOps = 0;
            _pendingBaseRowGroups.Clear();
        }

        public void EndBulk()
        {
            if (_bulkBatch == null) return;

            var batch = _bulkBatch;
            var ops = _bulkBatchOps;
            _bulkBatch = null;
            _bulkBatchOps = 0;

            try
            {
                FlushWindow(batch, ops);
                if (_dirty) FlushColumnFamily();
                _epochsSinceFlush = 0;
            }
            finally
            {
                batch.Dispose();
            }
        }

        public FilterMapsRange ReadRange()
        {
            var value = _manager.Get(_cf, FilterMapsSchema.RangeKey());
            return value == null ? null : FilterMapsRange.Decode(value);
        }

        public void WriteRange(FilterMapsRange range)
        {
            if (TryStageBatch(FilterMapsSchema.RangeKey(), range.Encode()))
            {
                var batch = _bulkBatch;
                var ops = _bulkBatchOps;
                _bulkBatch = new WriteBatch();
                _bulkBatchOps = 0;
                try
                {
                    FlushWindow(batch, ops);
                    MaybeFlushColumnFamilyPeriodically();
                }
                finally
                {
                    batch.Dispose();
                }
                return;
            }

            _manager.Put(_cf, FilterMapsSchema.RangeKey(), range.Encode());
        }

        public byte[] ReadBaseRowGroup(long mapRowIndex)
            => _manager.Get(_cf, FilterMapsSchema.BaseRowKey(mapRowIndex));

        public void WriteBaseRowGroup(long mapRowIndex, byte[] value)
        {
            if (TryStageBaseRowGroup(mapRowIndex, value)) return;
            WriteOrDelete(FilterMapsSchema.BaseRowKey(mapRowIndex), value);
        }

        public byte[] ReadExtRow(long mapRowIndex)
            => _manager.Get(_cf, FilterMapsSchema.ExtRowKey(mapRowIndex));

        public void WriteExtRow(long mapRowIndex, byte[] value)
        {
            if (TryStageBatch(FilterMapsSchema.ExtRowKey(mapRowIndex), value)) return;
            WriteOrDelete(FilterMapsSchema.ExtRowKey(mapRowIndex), value);
        }

        public (long blockNumber, byte[] blockId)? ReadLastBlockOfMap(long mapIndex)
        {
            var value = _manager.Get(_cf, FilterMapsSchema.LastBlockOfMapKey(mapIndex));
            if (value == null || value.Length != 40) return null;

            var blockId = new byte[32];
            System.Buffer.BlockCopy(value, 8, blockId, 0, 32);
            return ((long)RocksDbManager.Read64BE(value), blockId);
        }

        public void WriteLastBlockOfMap(long mapIndex, long blockNumber, byte[] blockId)
        {
            var value = new byte[40];
            var numberBytes = RocksDbManager.Write64BE(unchecked((ulong)blockNumber));
            System.Buffer.BlockCopy(numberBytes, 0, value, 0, 8);
            if (blockId != null)
            {
                System.Buffer.BlockCopy(blockId, 0, value, 8, blockId.Length);
            }

            var key = FilterMapsSchema.LastBlockOfMapKey(mapIndex);
            if (TryStageBatch(key, value)) return;
            _manager.Put(_cf, key, value);
        }

        public long? ReadBlockLvPointer(long blockNumber)
        {
            var value = _manager.Get(_cf, FilterMapsSchema.BlockLvPointerKey(blockNumber));
            return value == null ? (long?)null : (long)RocksDbManager.Read64BE(value);
        }

        public void WriteBlockLvPointer(long blockNumber, long lvPointer)
        {
            var key = FilterMapsSchema.BlockLvPointerKey(blockNumber);
            var value = RocksDbManager.Write64BE(unchecked((ulong)lvPointer));
            if (TryStageBatch(key, value)) return;
            _manager.Put(_cf, key, value);
        }

        private bool TryStageBatch(byte[] key, byte[] value)
        {
            if (_bulkBatch == null) return false;

            if (value == null || value.Length == 0)
                _bulkBatch.Delete(key, _cfHandle);
            else
                _bulkBatch.Put(key, value, _cfHandle);
            _bulkBatchOps++;
            _dirty = true;
            return true;
        }

        private bool TryStageBaseRowGroup(long mapRowIndex, byte[] value)
        {
            if (_bulkBatch == null) return false;

            var key = FilterMapsSchema.BaseRowKey(mapRowIndex);
            if (value == null || value.Length == 0)
            {
                _bulkBatch.Delete(key, _cfHandle);
                _bulkBatchOps++;
            }
            else
            {
                _pendingBaseRowGroups.Add(new KeyValuePair<byte[], byte[]>(key, value));
            }
            _dirty = true;
            return true;
        }

        private void WriteOrDelete(byte[] key, byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                _manager.Delete(_cf, key);
            }
            else
            {
                _manager.Put(_cf, key, value);
            }
        }

        private void FlushWindow(WriteBatch batch, int ops)
        {
            IngestPendingBaseRowGroups();
            if (ops > 0)
            {
                _manager.Write(batch, WalOffOptions);
            }
        }

        private void IngestPendingBaseRowGroups()
        {
            if (_pendingBaseRowGroups.Count == 0) return;

            try
            {
                _pendingBaseRowGroups.Sort((a, b) => CompareKeys(a.Key, b.Key));

                var scratchDir = Path.Combine(_manager.Options.DatabasePath, "fm-sst-scratch");
                Directory.CreateDirectory(scratchDir);
                var path = Path.Combine(scratchDir, $"fm_base_{Interlocked.Increment(ref _sstCounter)}.sst");

                using (var writer = new SstFileWriter(new EnvOptions(), RocksProfiles.IndexSstOptions()))
                {
                    writer.Open(path);
                    byte[] prev = null;
                    foreach (var kv in _pendingBaseRowGroups)
                    {
                        if (prev != null && CompareKeys(prev, kv.Key) == 0) continue;
                        writer.Put(kv.Key, kv.Value);
                        prev = kv.Key;
                    }
                    writer.Finish();
                }

                try
                {
                    using var lease = _manager.Lease();
                    lease.Database.IngestExternalFiles(new[] { path }, DefaultIngest, _cfHandle);
                }
                finally
                {
                    try { File.Delete(path); } catch { }
                }
            }
            finally
            {
                _pendingBaseRowGroups.Clear();
            }
        }

        private void MaybeFlushColumnFamilyPeriodically()
        {
            if (++_epochsSinceFlush < FlushEveryEpochs) return;
            if (_dirty) FlushColumnFamily();
            _epochsSinceFlush = 0;
        }

        public void FlushIfDirty()
        {
            if (_dirty) FlushColumnFamily();
        }

        private void FlushColumnFamily()
        {
            var flushOptions = new FlushOptions();
            using (var lease = _manager.Lease())
                RocksDbSharp.Native.Instance.rocksdb_flush_cf(lease.Database.Handle, flushOptions.Handle, _cfHandle.Handle);
            ColumnFamilyFlushCount++;
            _dirty = false;
        }

        private static int CompareKeys(byte[] a, byte[] b)
        {
            var len = a.Length < b.Length ? a.Length : b.Length;
            for (var i = 0; i < len; i++)
            {
                var d = a[i].CompareTo(b[i]);
                if (d != 0) return d;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
