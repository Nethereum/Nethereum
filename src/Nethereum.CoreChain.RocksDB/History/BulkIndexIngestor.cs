using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.History
{
    public sealed class BulkIndexIngestor
    {
        private static readonly IngestExternalFileOptions DefaultIngest = new IngestExternalFileOptions();

        public const int DefaultCheckpointMaxPendingEntries = 2_000_000;

        public const long PerEntryResidentOverheadBytesEstimate = 68;

        public const long DefaultResidentCeilingBytes = 6L * 1024 * 1024 * 1024;

        public const int ParallelSortThreshold = 200_000;

        private readonly RocksDb _db;
        private readonly string _scratchDir;
        private readonly Action<ulong> _progressCommit;
        private readonly int _checkpointIntervalBlocks;
        private readonly TimeSpan _checkpointInterval;
        private readonly int _maxPendingEntries;
        private readonly long _maxPendingBytes;
        private readonly long? _maxResidentBytes;
        private readonly bool _forceCheckpointOnFirstAccumulate;
        private readonly int _sortDegreeOfParallelism;
        private readonly ColumnFamilyHandle[] _compactionCfs;
        private long _sstCounter;
        private readonly Dictionary<string, long> _sstRunsByCf = new();

        private readonly object _writerGate = new object();
        private readonly Dictionary<string, ColumnFamilyHandle> _cfByName = new();
        private readonly Dictionary<string, List<KeyValuePair<byte[], byte[]>>> _pending = new();
        private readonly Stopwatch _sincePending = new();
        private readonly ContiguousFrontierTracker _frontier = new();
        private int _pendingBlocks;
        private long _pendingBytes;
        private bool _checkpointedSinceOpen;

        private readonly Action<string> _stepLog;

        public BulkIndexIngestor(
            RocksDb db, string scratchDir, IReadOnlyList<string> compactionColumnFamilies,
            Action<ulong> progressCommit,
            int checkpointIntervalBlocks, int checkpointIntervalSeconds,
            int maxPendingEntries = DefaultCheckpointMaxPendingEntries,
            Action<string> stepLog = null,
            long maxPendingBytes = long.MaxValue,
            long? maxResidentBytes = null,
            bool forceCheckpointOnFirstAccumulate = true,
            int sortDegreeOfParallelism = 0)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _scratchDir = scratchDir ?? throw new ArgumentNullException(nameof(scratchDir));
            Directory.CreateDirectory(_scratchDir);
            _stepLog = stepLog;
            _progressCommit = progressCommit ?? throw new ArgumentNullException(nameof(progressCommit));
            if (compactionColumnFamilies == null || compactionColumnFamilies.Count == 0)
                throw new ArgumentException("At least one column family is required.", nameof(compactionColumnFamilies));
            _checkpointIntervalBlocks = checkpointIntervalBlocks < 1 ? 1 : checkpointIntervalBlocks;
            _checkpointInterval = checkpointIntervalSeconds > 0
                ? TimeSpan.FromSeconds(checkpointIntervalSeconds)
                : TimeSpan.Zero;
            _maxPendingEntries = maxPendingEntries < 1 ? 1 : maxPendingEntries;
            _maxPendingBytes = maxPendingBytes;
            _maxResidentBytes = maxResidentBytes;
            _forceCheckpointOnFirstAccumulate = forceCheckpointOnFirstAccumulate;
            _sortDegreeOfParallelism = Math.Max(1, sortDegreeOfParallelism == 0 ? Environment.ProcessorCount : sortDegreeOfParallelism);

            _compactionCfs = new ColumnFamilyHandle[compactionColumnFamilies.Count];
            for (var i = 0; i < compactionColumnFamilies.Count; i++)
                _compactionCfs[i] = Cf(compactionColumnFamilies[i]);
        }

        public bool HasPendingWindow { get { lock (_writerGate) return _pendingBlocks > 0; } }

        public bool Accumulate(int blockCount, ulong windowHead, IReadOnlyList<(string Cf, byte[] Key, byte[] Value)> entries)
        {
            lock (_writerGate)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (!_pending.TryGetValue(e.Cf, out var list))
                        _pending[e.Cf] = list = new List<KeyValuePair<byte[], byte[]>>();
                    list.Add(new KeyValuePair<byte[], byte[]>(e.Key, e.Value));
                    _pendingBytes += e.Key.Length + e.Value.Length;
                }
                _pendingBlocks += blockCount;
                if (blockCount > 0) _frontier.Record(windowHead - (ulong)(blockCount - 1), windowHead);
                if (!_sincePending.IsRunning) _sincePending.Restart();

                if (_maxResidentBytes.HasValue)
                {
                    var resident = _pendingBytes + PendingEntryCount() * PerEntryResidentOverheadBytesEstimate;
                    if (resident > _maxResidentBytes.Value)
                        throw new InvalidOperationException(
                            $"BulkIndexIngestor pending run resident estimate {resident} bytes exceeds configured ceiling {_maxResidentBytes.Value} bytes.");
                }

                return (_forceCheckpointOnFirstAccumulate && !_checkpointedSinceOpen)
                    || _pendingBlocks >= _checkpointIntervalBlocks
                    || PendingEntryCount() >= _maxPendingEntries
                    || _pendingBytes >= _maxPendingBytes
                    || (_checkpointInterval > TimeSpan.Zero && _sincePending.Elapsed >= _checkpointInterval);
            }
        }

        public void Checkpoint()
        {
            lock (_writerGate)
            {
                if (_pendingBlocks == 0) return;
                CheckpointCore();
            }
        }

        private void CheckpointCore()
        {
            foreach (var kv in _pending) IngestSorted(kv.Value, kv.Key, Cf(kv.Key));

            _progressCommit(_frontier.Frontier);

            foreach (var kv in _pending) kv.Value.Clear();
            _pendingBlocks = 0;
            _pendingBytes = 0;
            _sincePending.Reset();
            _checkpointedSinceOpen = true;
        }

        public const long FinishCompactionPendingThresholdBytes = 4L * 1024 * 1024 * 1024;

        public void Finish()
        {
            lock (_writerGate)
            {
                if (_pendingBlocks > 0) CheckpointCore();
                foreach (var cf in _compactionCfs)
                    if (NeedsFinalCompaction(cf))
                        RocksDbManager.CompactRangeCfParallel(_db, cf, Environment.ProcessorCount);
            }
        }

        private bool NeedsFinalCompaction(ColumnFamilyHandle cf)
            => ExceedsFinalCompactionThreshold(_db.GetProperty("rocksdb.estimate-pending-compaction-bytes", cf));

        internal static bool ExceedsFinalCompactionThreshold(string pendingCompactionBytesProperty)
            => !long.TryParse(pendingCompactionBytesProperty, out var pending)
               || pending > FinishCompactionPendingThresholdBytes;

        private int PendingEntryCount()
        {
            var n = 0;
            foreach (var kv in _pending) n += kv.Value.Count;
            return n;
        }

        private void IngestSorted(List<KeyValuePair<byte[], byte[]>> entries, string cfName, ColumnFamilyHandle cf)
        {
            if (entries.Count == 0) return;

            long runSerializedBytes = 0;
            long sstRunsForCf = 0;
            if (_stepLog != null)
            {
                for (var i = 0; i < entries.Count; i++)
                    runSerializedBytes += entries[i].Key.Length + entries[i].Value.Length;
                _sstRunsByCf.TryGetValue(cfName, out sstRunsForCf);
                sstRunsForCf++;
                _sstRunsByCf[cfName] = sstRunsForCf;
            }

            var sortSw = _stepLog == null ? null : Stopwatch.StartNew();
            var partitions = entries.Count >= ParallelSortThreshold && _sortDegreeOfParallelism > 1
                ? BuildSortPartitions(entries.Count, _sortDegreeOfParallelism)
                : null;
            if (partitions == null)
            {
                entries.Sort((x, y) => CompareBytes(x.Key, y.Key));
            }
            else
            {
                Parallel.For(0, partitions.Length, i =>
                {
                    var part = partitions[i];
                    CollectionsMarshal.AsSpan(entries).Slice(part.Start, part.Length)
                        .Sort((a, b) => CompareBytes(a.Key, b.Key));
                });
            }
            sortSw?.Stop();

            var path = Path.Combine(_scratchDir, $"ingest_{Interlocked.Increment(ref _sstCounter)}.sst");
            var writeSw = _stepLog == null ? null : Stopwatch.StartNew();
            long sstBytes = 0;
            using (var writer = new SstFileWriter(new EnvOptions(), RocksProfiles.IndexSstOptions()))
            {
                writer.Open(path);
                if (partitions == null)
                {
                    byte[] prev = null;
                    foreach (var kv in entries)
                    {
                        if (prev != null && CompareBytes(prev, kv.Key) == 0) continue;
                        writer.Put(kv.Key, kv.Value);
                        prev = kv.Key;
                    }
                }
                else
                {
                    MergeSortedPartitionsIntoWriter(entries, partitions, writer);
                }
                writer.Finish();
            }
            writeSw?.Stop();
            if (_stepLog != null) { try { sstBytes = new FileInfo(path).Length; } catch { } }

            var ingestSw = _stepLog == null ? null : Stopwatch.StartNew();
            try
            {
                _db.IngestExternalFiles(new[] { path }, DefaultIngest, cf);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
            ingestSw?.Stop();

            if (_stepLog != null)
            {
                var pending = _db.GetProperty("rocksdb.estimate-pending-compaction-bytes", cf);
                _stepLog(
                    $"run cf={cfName} entries={entries.Count} sst={sstBytes / (1024.0 * 1024.0):F1}MB " +
                    $"sort={sortSw.ElapsedMilliseconds}ms write={writeSw.ElapsedMilliseconds}ms ingest={ingestSw.ElapsedMilliseconds}ms " +
                    $"pending_compaction={ToGb(pending):F2}GB run_serialized_bytes={runSerializedBytes} sst_runs_cf={sstRunsForCf}");
            }
        }

        private static (int Start, int Length)[] BuildSortPartitions(int count, int degreeOfParallelism)
        {
            var p = Math.Max(1, Math.Min(degreeOfParallelism, count));
            var bounds = new (int Start, int Length)[p];
            var baseLen = count / p;
            var remainder = count % p;
            var start = 0;
            for (var i = 0; i < p; i++)
            {
                var len = baseLen + (i < remainder ? 1 : 0);
                bounds[i] = (start, len);
                start += len;
            }
            return bounds;
        }

        private static void MergeSortedPartitionsIntoWriter(
            List<KeyValuePair<byte[], byte[]>> entries, (int Start, int Length)[] partitions, SstFileWriter writer)
        {
            var cursors = new int[partitions.Length];
            var ends = new int[partitions.Length];
            var heap = new PriorityQueue<int, byte[]>(Comparer<byte[]>.Create(CompareBytes));
            for (var i = 0; i < partitions.Length; i++)
            {
                cursors[i] = partitions[i].Start;
                ends[i] = partitions[i].Start + partitions[i].Length;
                if (cursors[i] < ends[i]) heap.Enqueue(i, entries[cursors[i]].Key);
            }

            byte[] prev = null;
            while (heap.Count > 0)
            {
                var i = heap.Dequeue();
                var kv = entries[cursors[i]];
                cursors[i]++;
                if (prev == null || CompareBytes(prev, kv.Key) != 0)
                {
                    writer.Put(kv.Key, kv.Value);
                    prev = kv.Key;
                }
                if (cursors[i] < ends[i]) heap.Enqueue(i, entries[cursors[i]].Key);
            }
        }

        private static double ToGb(string bytes) => long.TryParse(bytes, out var b) ? b / (1024.0 * 1024.0 * 1024.0) : -1;

        private ColumnFamilyHandle Cf(string name)
            => _cfByName.TryGetValue(name, out var h) ? h : (_cfByName[name] = _db.GetColumnFamily(name));

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int k = 0; k < len; k++) { int d = a[k].CompareTo(b[k]); if (d != 0) return d; }
            return a.Length.CompareTo(b.Length);
        }

        private sealed class ContiguousFrontierTracker
        {
            private readonly List<(ulong Start, ulong End)> _parked = new();
            private bool _hasFrontier;
            private ulong _frontier;

            public ulong Frontier => _frontier;

            public void Record(ulong start, ulong end)
            {
                if (end < start) return;

                if (!_hasFrontier)
                {
                    _hasFrontier = true;
                    _frontier = start;
                }
                _parked.Add((start, end));
                Extend();
            }

            private void Extend()
            {
                _parked.Sort((a, b) => a.Start.CompareTo(b.Start));
                bool progressed;
                do
                {
                    progressed = false;
                    for (var i = _parked.Count - 1; i >= 0; i--)
                    {
                        var (start, end) = _parked[i];
                        if (end <= _frontier) { _parked.RemoveAt(i); continue; }
                        if (start > _frontier + 1) continue;

                        _frontier = end;
                        _parked.RemoveAt(i);
                        progressed = true;
                    }
                } while (progressed);
            }
        }
    }
}
