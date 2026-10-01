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

        public RocksDbWritePressureMonitor(RocksDbManager rocks, string dataDir)
            : this(rocks, dataDir, rocks, null) { }

        public RocksDbWritePressureMonitor(RocksDbManager rocks, string dataDir, RocksDbManager historyRocks)
            : this(rocks, dataDir, historyRocks, null) { }

        public RocksDbWritePressureMonitor(
            RocksDbManager rocks, string dataDir, RocksDbManager historyRocks, RocksDbManager freezerHistoryRocks)
            : this(rocks, dataDir, DefaultReader(rocks), DefaultReader(historyRocks ?? rocks),
                   freezerHistoryRocks == null ? null : DefaultReader(freezerHistoryRocks)) { }

        public RocksDbWritePressureMonitor(string dataDir, Func<string, string, string> readProperty)
            : this(null, dataDir, readProperty, readProperty, null) { }

        public RocksDbWritePressureMonitor(
            string dataDir, Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty)
            : this(null, dataDir, readProperty, readHistoryProperty, null) { }

        public RocksDbWritePressureMonitor(
            string dataDir, Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty,
            Func<string, string, string> readFreezerHistoryProperty)
            : this(null, dataDir, readProperty, readHistoryProperty, readFreezerHistoryProperty) { }

        private RocksDbWritePressureMonitor(
            RocksDbManager rocks, string dataDir,
            Func<string, string, string> readProperty, Func<string, string, string> readHistoryProperty,
            Func<string, string, string> readFreezerHistoryProperty)
        {
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
        {
            long worst = 0;
            foreach (var cf in FreezerIndexingLiveIndexCfs)
            {
                var raw = _readFreezerHistoryProperty("rocksdb.estimate-pending-compaction-bytes", cf);
                if (raw != null && long.TryParse(raw, out var bytes) && bytes > worst) worst = bytes;
            }
            return worst;
        }

        private long WorstFreezerIndexingL0Files()
        {
            long worst = 0;
            foreach (var cf in FreezerIndexingLiveIndexCfs)
            {
                var raw = _readFreezerHistoryProperty("rocksdb.num-files-at-level0", cf);
                if (raw != null && long.TryParse(raw, out var files) && files > worst) worst = files;
            }
            return worst;
        }

        private bool EvaluateFreezerIndexingWriteStall()
        {
            bool hardStopped = AnyFreezerIndexingCfWriteStopped();
            bool saturating = EvaluateHysteresisPause(
                WorstFreezerIndexingImmutableMemtables(), _freezerIndexingStallPaused,
                FreezerIndexingImmutableMemtablePauseCount, FreezerIndexingImmutableMemtableResumeCount);
            return hardStopped || saturating;
        }

        private bool AnyFreezerIndexingCfWriteStopped()
        {
            foreach (var cf in FreezerIndexingLiveIndexCfs)
            {
                var raw = _readFreezerHistoryProperty(PropIsWriteStopped, cf);
                if (raw != null && long.TryParse(raw, out var stopped) && stopped != 0) return true;
            }
            return false;
        }

        private long WorstFreezerIndexingImmutableMemtables()
        {
            long worst = 0;
            foreach (var cf in FreezerIndexingLiveIndexCfs)
            {
                var raw = _readFreezerHistoryProperty(PropNumImmutableMemtable, cf);
                if (raw != null && long.TryParse(raw, out var n) && n > worst) worst = n;
            }
            return worst;
        }

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
        {
            long worst = 0;
            foreach (var cf in HistoryBulkCfs)
            {
                var raw = _readHistoryProperty("rocksdb.estimate-pending-compaction-bytes", cf);
                if (raw != null && long.TryParse(raw, out var bytes) && bytes > worst) worst = bytes;
            }
            return worst;
        }

        private long WorstHistoryL0Files()
        {
            long worst = 0;
            foreach (var cf in HistoryBulkCfs)
            {
                var raw = _readHistoryProperty("rocksdb.num-files-at-level0", cf);
                if (raw != null && long.TryParse(raw, out var files) && files > worst) worst = files;
            }
            return worst;
        }

        private bool EvaluateHistoryWriteStall()
        {
            bool hardStopped = AnyHistoryCfWriteStopped();
            bool saturating = EvaluateHysteresisPause(
                WorstHistoryImmutableMemtables(), _historyStallPaused,
                HistoryImmutableMemtablePauseCount, HistoryImmutableMemtableResumeCount);
            return hardStopped || saturating;
        }

        private bool AnyHistoryCfWriteStopped()
        {
            foreach (var cf in HistoryBulkCfs)
            {
                var raw = _readHistoryProperty(PropIsWriteStopped, cf);
                if (raw != null && long.TryParse(raw, out var stopped) && stopped != 0) return true;
            }
            return false;
        }

        private long WorstHistoryImmutableMemtables()
        {
            long worst = 0;
            foreach (var cf in HistoryBulkCfs)
            {
                var raw = _readHistoryProperty(PropNumImmutableMemtable, cf);
                if (raw != null && long.TryParse(raw, out var n) && n > worst) worst = n;
            }
            return worst;
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
        {
            bool hardStopped = AnyStateCfWriteStopped();
            bool saturating = EvaluateHysteresisPause(
                WorstStateImmutableMemtables(), _stateStallPaused,
                StateImmutableMemtablePauseCount, StateImmutableMemtableResumeCount);
            return hardStopped || saturating;
        }

        private bool AnyStateCfWriteStopped()
        {
            foreach (var cf in StateWriteCfs)
            {
                var raw = _readProperty(PropIsWriteStopped, cf);
                if (raw != null && long.TryParse(raw, out var stopped) && stopped != 0) return true;
            }
            return false;
        }

        private long WorstStateImmutableMemtables()
        {
            long worst = 0;
            foreach (var cf in StateWriteCfs)
            {
                var raw = _readProperty(PropNumImmutableMemtable, cf);
                if (raw != null && long.TryParse(raw, out var n) && n > worst) worst = n;
            }
            return worst;
        }

        private long WorstStateDebtBytes()
        {
            long worst = 0;
            foreach (var cf in StateWriteCfs)
            {
                var raw = _readProperty("rocksdb.estimate-pending-compaction-bytes", cf);
                if (raw != null && long.TryParse(raw, out var bytes) && bytes > worst) worst = bytes;
            }
            return worst;
        }

        private long WorstStateL0Files()
        {
            long worst = 0;
            foreach (var cf in StateLeveledCfs)
            {
                var raw = _readProperty("rocksdb.num-files-at-level0", cf);
                if (raw != null && long.TryParse(raw, out var files) && files > worst) worst = files;
            }
            return worst;
        }

        public Task CompactStateAsync(Action<string> progress, CancellationToken ct)
            => Task.Run(() =>
            {
                foreach (var cf in StateWriteCfs)
                {
                    ct.ThrowIfCancellationRequested();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    progress?.Invoke($"compacting {cf}...");
                    _rocks.CompactColumnFamilyParallel(cf, subcompactions: 4);
                    progress?.Invoke($"{cf} compacted in {sw.Elapsed:hh\\:mm\\:ss}");
                }
            }, ct);

        private const long EngineL0PauseFiles = 15;
        private const long EngineL0ResumeFiles = 9;
        private bool _enginePressurePaused;

        private string EngineWritePressure()
        {
            long worstFiles = 0;
            string worstCf = null;
            foreach (var cf in EngineLeveledCfs)
            {
                var raw = _readProperty("rocksdb.num-files-at-level0", cf);
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
