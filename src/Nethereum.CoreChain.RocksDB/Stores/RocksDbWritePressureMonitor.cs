using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbWritePressureMonitor
    {
        private readonly RocksDbManager _rocks;
        private readonly string _dataDir;

        private readonly Func<string, string, string> _readProperty;

        private readonly Func<string, string, string> _readHistoryProperty;

        private readonly Func<string, string, string> _readFreezerHistoryProperty;

        private readonly Func<string> _probeStateWrite;
        private readonly Func<string> _probeHistoryWrite;
        private readonly Func<string> _probeFreezerHistoryWrite;

        private const ulong CaughtUpWithinBlocks = 2;
        private const ulong FallenBehindBlocks = 32;
        private readonly object _yieldGate = new object();
        private bool _yielding = true;
        private ulong _lastHead;
        private ulong _lastTip;

        public RocksDbWritePressureMonitor(RocksDbManager rocks, string dataDir)
            : this(rocks, dataDir, rocks, null) { }

        public RocksDbWritePressureMonitor(RocksDbManager rocks, string dataDir, RocksDbManager historyRocks)
            : this(rocks, dataDir, historyRocks, null) { }

        public RocksDbWritePressureMonitor(
            RocksDbManager rocks, string dataDir, RocksDbManager historyRocks, RocksDbManager freezerHistoryRocks)
            : this(rocks, dataDir, DefaultReader(rocks), DefaultReader(historyRocks ?? rocks),
                   freezerHistoryRocks == null ? null : DefaultReader(freezerHistoryRocks),
                   rocks.TryProbeWrite, (historyRocks ?? rocks).TryProbeWrite,
                   freezerHistoryRocks == null ? null : freezerHistoryRocks.TryProbeWrite) { }

        public RocksDbWritePressureMonitor(string dataDir, Func<string, string, string> readProperty)
            : this(null, dataDir, readProperty, readProperty, null, null, null, null) { }

        public RocksDbWritePressureMonitor(
            string dataDir, Func<string, string, string> readProperty, Func<string> probeWrite)
            : this(null, dataDir, readProperty, readProperty, null, probeWrite, probeWrite, null) { }

        public RocksDbWritePressureMonitor(
            string dataDir, Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty)
            : this(null, dataDir, readProperty, readHistoryProperty, null, null, null, null) { }

        public RocksDbWritePressureMonitor(
            string dataDir, Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty,
            Func<string, string, string> readFreezerHistoryProperty)
            : this(null, dataDir, readProperty, readHistoryProperty, readFreezerHistoryProperty, null, null, null) { }

        private RocksDbWritePressureMonitor(
            RocksDbManager rocks, string dataDir,
            Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty,
            Func<string, string, string> readFreezerHistoryProperty,
            Func<string> probeStateWrite, Func<string> probeHistoryWrite, Func<string> probeFreezerHistoryWrite)
        {
            _probeStateWrite = probeStateWrite;
            _probeHistoryWrite = probeHistoryWrite;
            _probeFreezerHistoryWrite = probeFreezerHistoryWrite;
            _rocks = rocks;
            _dataDir = dataDir;
            _readProperty = readProperty;
            _readHistoryProperty = readHistoryProperty;
            _readFreezerHistoryProperty = readFreezerHistoryProperty;
        }

        private static Func<string, string, string> DefaultReader(RocksDbManager rocks)
            => (property, cf) =>
            {
                using var lease = rocks.Lease();
                return cf == null
                    ? lease.Database.GetProperty(property)
                    : lease.Database.GetProperty(property, rocks.GetColumnFamily(cf));
            };

        private static readonly string[] HistoryBulkCfs =
        {
            History.HistoryColumnFamilies.TxBody,
            History.HistoryColumnFamilies.ReceiptBody,
            History.HistoryColumnFamilies.BlockHeader,
            History.HistoryColumnFamilies.BlockMeta,
            History.HistoryColumnFamilies.TxHashIndex,
            History.HistoryColumnFamilies.BlockHashIndex,
        };


        private const long HistoryDebtPauseBytes = 60L * 1024 * 1024 * 1024;
        private const long HistoryDebtResumeBytes = 10L * 1024 * 1024 * 1024;
        private bool _historyWritesPaused;

        private const long HistoryL0PauseFiles = 12;
        private const long HistoryL0ResumeFiles = 6;
        private bool _historyL0Paused;

        private const long HistoryImmutableMemtablePauseCount = 1;
        private const long HistoryImmutableMemtableResumeCount = 1;
        private bool _historyStallPaused;

        public bool ShouldPauseHistoryWrites()
        {
            _historyWritesPaused = EvaluateHysteresisPause(
                WorstHistoryDebtBytes(), _historyWritesPaused, HistoryDebtPauseBytes, HistoryDebtResumeBytes);
            _historyL0Paused = EvaluateHysteresisPause(
                WorstHistoryL0Files(), _historyL0Paused, HistoryL0PauseFiles, HistoryL0ResumeFiles);
            _historyStallPaused = EvaluateHistoryWriteStall();
            return _historyWritesPaused || _historyL0Paused || _historyStallPaused || EngineWritePressure() != null;
        }

        public string DescribeHistoryBackpressure()
        {
            if (_historyStallPaused)
                return AnyHistoryCfWriteStopped()
                    ? $"history WRITE-STOP: rocksdb.is-write-stopped=1 on a history CF (flush pipeline saturated, " +
                      $"immutable_memtables={WorstHistoryImmutableMemtables()}) — pausing history fill so the flush lane can drain before a consumer native-blocks"
                    : $"history flush-pipeline saturation: {WorstHistoryImmutableMemtables()} immutable memtables " +
                      $"(pause > {HistoryImmutableMemtablePauseCount}, resume <= {HistoryImmutableMemtableResumeCount}; a DB-wide write-STOP is imminent)";
            if (EngineWritePressure() is string enginePressure) return enginePressure;
            if (_historyWritesPaused)
                return $"history compaction debt {WorstHistoryDebtBytes() / 1073741824.0:F1} GB " +
                       $"(pause > {HistoryDebtPauseBytes / 1073741824} GB, resume < {HistoryDebtResumeBytes / 1073741824} GB; engine brake at 128 GB)";
            return $"history level-0 backlog {WorstHistoryL0Files()} files " +
                   $"(pause > {HistoryL0PauseFiles}, resume < {HistoryL0ResumeFiles}; engine slows writes near 20)";
        }

        private static readonly string[] FreezerIndexingLiveIndexCfs =
        {
            History.HistoryColumnFamilies.BlockHashIndex,
            History.HistoryColumnFamilies.TxHashIndex,
            History.HistoryColumnFamilies.LogFilterMaps,
        };

        private const long FreezerIndexingDebtPauseBytes = 24L * 1024 * 1024 * 1024;
        private const long FreezerIndexingDebtResumeBytes = 8L * 1024 * 1024 * 1024;
        private bool _freezerIndexingDebtPaused;

        private const long FreezerIndexingL0PauseFiles = History.RocksProfiles.LiveIndexL0SlowdownTrigger;
        private const long FreezerIndexingL0ResumeFiles = History.RocksProfiles.LiveIndexL0CompactionTrigger;
        private bool _freezerIndexingL0Paused;

        private const long FreezerIndexingImmutableMemtablePauseCount = 1;
        private const long FreezerIndexingImmutableMemtableResumeCount = 1;
        private bool _freezerIndexingStallPaused;

        private const string FreezerIndexingPauseFileName = "freezer-indexing.paused";
        private string FreezerIndexingPauseFilePath => Path.Combine(_dataDir, FreezerIndexingPauseFileName);

        private bool OperatorPausedFreezerIndexing()
        {
            try { return File.Exists(FreezerIndexingPauseFilePath); }
            catch { return false; }
        }

        public bool ShouldPauseFreezerIndexing()
        {
            if (_readFreezerHistoryProperty == null) return false;
            if (OperatorPausedFreezerIndexing()) return true;

            _freezerIndexingDebtPaused = EvaluateHysteresisPause(
                WorstFreezerIndexingDebtBytes(), _freezerIndexingDebtPaused,
                FreezerIndexingDebtPauseBytes, FreezerIndexingDebtResumeBytes);
            _freezerIndexingL0Paused = EvaluateHysteresisPause(
                WorstFreezerIndexingL0Files(), _freezerIndexingL0Paused,
                FreezerIndexingL0PauseFiles, FreezerIndexingL0ResumeFiles);
            _freezerIndexingStallPaused = EvaluateFreezerIndexingWriteStall();
            return _freezerIndexingDebtPaused || _freezerIndexingL0Paused || _freezerIndexingStallPaused;
        }

        public string DescribeFreezerIndexingBackpressure()
        {
            if (OperatorPausedFreezerIndexing())
                return $"operator pause switch present ({FreezerIndexingPauseFilePath}) — index trailer idled; " +
                       "the freezer files keep writing";
            if (_freezerIndexingStallPaused)
                return AnyFreezerIndexingCfWriteStopped()
                    ? $"freezer-index WRITE-STOP: rocksdb.is-write-stopped=1 on a freezer-history CF (flush pipeline " +
                      $"saturated, immutable_memtables={WorstFreezerIndexingImmutableMemtables()}) — pausing the index trailer so the flush lane can drain"
                    : $"freezer-index flush-pipeline saturation: {WorstFreezerIndexingImmutableMemtables()} immutable memtables " +
                      $"(pause > {FreezerIndexingImmutableMemtablePauseCount}, resume <= {FreezerIndexingImmutableMemtableResumeCount})";
            if (_freezerIndexingDebtPaused)
                return $"freezer-index compaction debt {WorstFreezerIndexingDebtBytes() / 1073741824.0:F1} GB " +
                       $"(pause > {FreezerIndexingDebtPauseBytes / 1073741824} GB, resume < {FreezerIndexingDebtResumeBytes / 1073741824} GB)";
            return $"freezer-index level-0 backlog {WorstFreezerIndexingL0Files()} files " +
                   $"(pause > {FreezerIndexingL0PauseFiles}, resume < {FreezerIndexingL0ResumeFiles})";
        }

        private long WorstFreezerIndexingDebtBytes()
            => Worst(_readFreezerHistoryProperty, FreezerIndexingLiveIndexCfs, PropPendingCompactionBytes);

        private long WorstFreezerIndexingL0Files()
            => Worst(_readFreezerHistoryProperty, FreezerIndexingLiveIndexCfs, PropNumFilesAtLevel0);

        private bool EvaluateFreezerIndexingWriteStall()
            => EvaluateStall(_readFreezerHistoryProperty, FreezerIndexingLiveIndexCfs, _freezerIndexingStallPaused,
                FreezerIndexingImmutableMemtablePauseCount, FreezerIndexingImmutableMemtableResumeCount);

        private bool AnyFreezerIndexingCfWriteStopped()
            => AnyNonZero(_readFreezerHistoryProperty, FreezerIndexingLiveIndexCfs, PropIsWriteStopped);

        private long WorstFreezerIndexingImmutableMemtables()
            => Worst(_readFreezerHistoryProperty, FreezerIndexingLiveIndexCfs, PropNumImmutableMemtable);

        private const string BackfillPauseFileName = "backfill.paused";
        private string BackfillPauseFilePath => Path.Combine(_dataDir, BackfillPauseFileName);

        public bool ShouldPauseBackfill()
        {
            try { return File.Exists(BackfillPauseFilePath); }
            catch { return false; }
        }

        public string DescribeBackfillPause()
            => $"operator pause switch present ({BackfillPauseFilePath})";

        private long WorstHistoryDebtBytes()
            => Worst(_readHistoryProperty, HistoryBulkCfs, PropPendingCompactionBytes);

        private long WorstHistoryL0Files()
            => Worst(_readHistoryProperty, HistoryBulkCfs, PropNumFilesAtLevel0);

        private bool EvaluateHistoryWriteStall()
            => EvaluateStall(_readHistoryProperty, HistoryBulkCfs, _historyStallPaused,
                HistoryImmutableMemtablePauseCount, HistoryImmutableMemtableResumeCount);

        private bool AnyHistoryCfWriteStopped()
            => AnyNonZero(_readHistoryProperty, HistoryBulkCfs, PropIsWriteStopped);

        private long WorstHistoryImmutableMemtables()
            => Worst(_readHistoryProperty, HistoryBulkCfs, PropNumImmutableMemtable);

        private static long Worst(Func<string, string, string> read, IEnumerable<string> cfs, string property)
        {
            long worst = 0;
            foreach (var cf in cfs)
            {
                var raw = read(property, cf);
                if (raw != null && long.TryParse(raw, out var value) && value > worst) worst = value;
            }
            return worst;
        }

        private static bool AnyNonZero(Func<string, string, string> read, IEnumerable<string> cfs, string property)
        {
            foreach (var cf in cfs)
            {
                var raw = read(property, cf);
                if (raw != null && long.TryParse(raw, out var value) && value != 0) return true;
            }
            return false;
        }

        private static bool EvaluateStall(
            Func<string, string, string> read, IEnumerable<string> cfs, bool wasPaused, long pauseAt, long resumeAt)
        {
            bool hardStopped = AnyNonZero(read, cfs, PropIsWriteStopped);
            bool saturating = EvaluateHysteresisPause(Worst(read, cfs, PropNumImmutableMemtable), wasPaused, pauseAt, resumeAt);
            return hardStopped || saturating;
        }

        public static bool EvaluateHysteresisPause(long worstBytes, bool currentlyPaused, long pauseAt, long resumeAt)
            => currentlyPaused ? worstBytes > resumeAt : worstBytes > pauseAt;

        private const long StateL0PauseFiles = 12;
        private const long StateL0ResumeFiles = 6;
        private const long StateDebtPauseBytes = 100L * 1024 * 1024 * 1024;
        private const long StateDebtResumeBytes = 60L * 1024 * 1024 * 1024;

        private const long StateImmutableMemtablePauseCount = 1;
        private const long StateImmutableMemtableResumeCount = 1;
        private const string PropIsWriteStopped = "rocksdb.is-write-stopped";
        private const string PropNumImmutableMemtable = "rocksdb.num-immutable-mem-table";
        private const string PropPendingCompactionBytes = "rocksdb.estimate-pending-compaction-bytes";
        private const string PropNumFilesAtLevel0 = "rocksdb.num-files-at-level0";

        private static readonly string[] StateWriteCfs =
        {
            RocksDbManager.CF_STATE_ACCOUNTS,
            RocksDbManager.CF_STATE_STORAGE,
            RocksDbManager.CF_STATE_TRIE_ACCOUNT,
            RocksDbManager.CF_STATE_TRIE_STORAGE,
        };

        private static readonly HashSet<string> UniversalCompactionCfs = new(
            LiveColumnFamilies.Catalogue
                .Where(cf => cf.Profile == LiveCfProfile.UniversalBulkPoint)
                .Select(cf => cf.Name));

        private static readonly string[] StateLeveledCfs =
            StateWriteCfs.Where(cf => !UniversalCompactionCfs.Contains(cf)).ToArray();

        private static readonly string[] EngineLeveledCfs =
            RocksDbManager.ColumnFamilyNames.Where(cf => !UniversalCompactionCfs.Contains(cf)).ToArray();

        private bool _stateWritesPaused;
        private bool _stateDebtPaused;
        private bool _stateStallPaused;

        public bool ShouldPauseStateWrites()
        {
            _stateWritesPaused = EvaluateHysteresisPause(
                WorstStateL0Files(), _stateWritesPaused, StateL0PauseFiles, StateL0ResumeFiles);
            _stateDebtPaused = EvaluateHysteresisPause(
                WorstStateDebtBytes(), _stateDebtPaused, StateDebtPauseBytes, StateDebtResumeBytes);
            _stateStallPaused = EvaluateStateWriteStall();
            return _stateWritesPaused || _stateDebtPaused || _stateStallPaused
                || EngineWritePressure() != null;
        }

        public string DescribeStateBackpressure()
        {
            if (_stateStallPaused)
                return AnyStateCfWriteStopped()
                    ? $"state WRITE-STOP: rocksdb.is-write-stopped=1 on a state CF (flush pipeline saturated, " +
                      $"immutable_memtables={WorstStateImmutableMemtables()}) — pausing leaf stream so the flush lane can drain before a consumer native-blocks"
                    : $"state flush-pipeline saturation: {WorstStateImmutableMemtables()} immutable memtables " +
                      $"(pause > {StateImmutableMemtablePauseCount}, resume <= {StateImmutableMemtableResumeCount}; a DB-wide write-STOP is imminent)";
            if (EngineWritePressure() is string enginePressure) return enginePressure;
            if (_stateDebtPaused && !_stateWritesPaused)
                return $"state compaction debt {WorstStateDebtBytes() / 1073741824.0:F1} GB " +
                       $"(pause > {StateDebtPauseBytes / 1073741824} GB, resume < {StateDebtResumeBytes / 1073741824} GB; engine brake at 128 GB)";
            return $"state level-0 backlog {WorstStateL0Files()} files " +
                   $"(pause > {StateL0PauseFiles}, resume < {StateL0ResumeFiles}; engine slows writes near 20)" +
                   (_stateDebtPaused ? $" + compaction debt {WorstStateDebtBytes() / 1073741824.0:F1} GB" : "");
        }

        private bool EvaluateStateWriteStall()
            => EvaluateStall(_readProperty, StateWriteCfs, _stateStallPaused,
                StateImmutableMemtablePauseCount, StateImmutableMemtableResumeCount);

        private bool AnyStateCfWriteStopped()
            => AnyNonZero(_readProperty, StateWriteCfs, PropIsWriteStopped);

        private long WorstStateImmutableMemtables()
            => Worst(_readProperty, StateWriteCfs, PropNumImmutableMemtable);

        private long WorstStateDebtBytes()
            => Worst(_readProperty, StateWriteCfs, PropPendingCompactionBytes);

        private long WorstStateL0Files()
            => Worst(_readProperty, StateLeveledCfs, PropNumFilesAtLevel0);

        public bool ShouldYieldToExecution(ulong executedHead, ulong trustedTip)
        {
            lock (_yieldGate)
            {
                _lastHead = executedHead;
                _lastTip = trustedTip;
                if (trustedTip == 0) return false;
                var behind = Nethereum.CoreChain.Storage.HeaderSubchains.BlocksBehind(executedHead, trustedTip);
                _yielding = EvaluateHysteresisPause(
                    (long)behind, _yielding, (long)FallenBehindBlocks, (long)CaughtUpWithinBlocks);
                return _yielding;
            }
        }

        public string DescribeYieldToExecution()
        {
            lock (_yieldGate)
            {
                var behind = Nethereum.CoreChain.Storage.HeaderSubchains.BlocksBehind(_lastHead, _lastTip);
                return $"yielding to block execution: executed head {_lastHead} is {behind} block(s) behind the trusted tip {_lastTip} (resumes within {CaughtUpWithinBlocks})";
            }
        }

        private const string PropBackgroundErrors = "rocksdb.background-errors";

        public string FatalBackgroundError()
        {
            var databases = new (string Name, Func<string, string, string> Read, Func<string> ProbeWrite)[]
            {
                ("state", _readProperty, _probeStateWrite),
                ("history", _readHistoryProperty, _probeHistoryWrite),
                ("freezer-history", _readFreezerHistoryProperty, _probeFreezerHistoryWrite),
            };
            foreach (var (name, read, probeWrite) in databases)
            {
                if (read == null) continue;
                try
                {
                    var raw = read(PropBackgroundErrors, null);
                    if (raw == null || !long.TryParse(raw, out var count) || count <= 0) continue;
                    var rejection = probeWrite == null ? "no write probe" : probeWrite();
                    if (rejection != null)
                        return $"{name} database reports {count} {PropBackgroundErrors} and rejects writes until it is reopened: {rejection}";
                }
                catch (ObjectDisposedException) { continue; }
            }
            return null;
        }

        public Task CompactStateAsync(Action<string> progress, CancellationToken ct)
            => Task.Factory.StartNew(() =>
            {
                foreach (var cf in StateWriteCfs)
                {
                    ct.ThrowIfCancellationRequested();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    progress?.Invoke($"compacting {cf}...");
                    _rocks.CompactColumnFamilyParallel(cf, 4, ct);
                    progress?.Invoke($"{cf} compacted in {sw.Elapsed:hh\\:mm\\:ss}");
                }
            }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        private const long EngineL0PauseFiles = 15;
        private const long EngineL0ResumeFiles = 9;
        private bool _enginePressurePaused;

        private string EngineWritePressure()
        {
            long worstFiles = 0;
            string worstCf = null;
            foreach (var cf in EngineLeveledCfs)
            {
                var raw = _readProperty(PropNumFilesAtLevel0, cf);
                if (raw != null && long.TryParse(raw, out var files) && files > worstFiles)
                {
                    worstFiles = files;
                    worstCf = cf;
                }
            }
            _enginePressurePaused = EvaluateHysteresisPause(
                worstFiles, _enginePressurePaused, EngineL0PauseFiles, EngineL0ResumeFiles);
            if (_enginePressurePaused)
                return $"engine pressure: {worstCf} at {worstFiles} level-0 files (slowdown trigger ~20) — all sync writers yielding";

            var delayed = _readProperty("rocksdb.actual-delayed-write-rate", null);
            if (delayed != null && ulong.TryParse(delayed, out var rate) && rate > 0)
                return $"engine pressure: writes already delayed to {rate / 1048576.0:F1} MB/s — all sync writers yielding";
            return null;
        }
    }
}
