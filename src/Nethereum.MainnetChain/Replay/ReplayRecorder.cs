using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Replay
{
    public sealed class ReplayRecorder : IConsensusBlockGate, ILoggerProvider
    {
        private const string TimingEvent = "block.exec.timing";
        private const string StatsEvent = "block.exec.stats";
        private const string RejectionEvent = "Block {Block} rejected";
        private const int MaxDiagnostics = 200;

        private readonly IConsensusBlockGate _inner;
        private readonly IFlushCadence _cadence;
        private readonly Func<RocksDbCacheStats>? _cacheProbe;
        private readonly Action<ulong, string> _onRejected;
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly ConcurrentDictionary<ulong, TimingEntry> _timings = new();
        private readonly ConcurrentDictionary<ulong, StatsEntry> _stats = new();
        private readonly List<ReplayBlockRecord> _blocks = new();
        private readonly List<ReplayCacheSnapshot> _cacheSnapshots = new();
        private readonly ConcurrentQueue<string> _diagnostics = new();
        private readonly Stopwatch _blockClock = new();
        private int _sinceBoundary;
        private long _allocBefore;
        private int _gen0Before;
        private int _gen1Before;
        private int _gen2Before;
        private ulong _inFlightBlock;

        public ReplayRecorder(
            IConsensusBlockGate inner,
            IFlushCadence cadence,
            ulong executedHead,
            Func<RocksDbCacheStats>? cacheProbe,
            Action<ulong, string> onRejected)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cadence = cadence ?? throw new ArgumentNullException(nameof(cadence));
            _cacheProbe = cacheProbe;
            _onRejected = onRejected ?? throw new ArgumentNullException(nameof(onRejected));
            _sinceBoundary = BlocksSinceBoundary(cadence, executedHead);
        }

        public IReadOnlyList<ReplayBlockRecord> Blocks => _blocks;
        public IReadOnlyList<ReplayCacheSnapshot> CacheSnapshots => _cacheSnapshots;
        public IReadOnlyCollection<string> Diagnostics => _diagnostics;
        public ulong? RejectedBlock { get; private set; }
        public string? RejectionReason { get; private set; }

        public async Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header, byte[] computedBlockHash, CancellationToken ct)
        {
            var verdict = await _inner.IsBlockCanonicalAsync(header, computedBlockHash, ct).ConfigureAwait(false);
            if (verdict.Accepted) BeginBlock((ulong)(System.Numerics.BigInteger)header.BlockNumber);
            return verdict;
        }

        public async Task OnBlockImportedAsync(BlockHeader header, byte[] blockHash, CancellationToken ct)
        {
            _blockClock.Stop();
            var wall = _blockClock.Elapsed.TotalMilliseconds;
            var number = (ulong)(System.Numerics.BigInteger)header.BlockNumber;
            if (number == _inFlightBlock) EndBlock(number, header, wall);
            await _inner.OnBlockImportedAsync(header, blockHash, ct).ConfigureAwait(false);
        }

        public void SnapshotCache(string label, ulong atBlock)
        {
            if (_cacheProbe == null) return;
            var stats = _cacheProbe();
            _cacheSnapshots.Add(new ReplayCacheSnapshot
            {
                Label = label,
                AtBlock = atBlock,
                IndexHit = stats.IndexHit,
                IndexMiss = stats.IndexMiss,
                FilterHit = stats.FilterHit,
                FilterMiss = stats.FilterMiss,
                DataHit = stats.DataHit,
                DataMiss = stats.DataMiss,
                UsageBytes = stats.UsageBytes,
            });
        }

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose() => _process.Dispose();

        private static int BlocksSinceBoundary(IFlushCadence cadence, ulong executedHead)
        {
            var since = 0;
            for (var block = executedHead; block > 0 && !cadence.IsBoundary(block); block--) since++;
            return since;
        }

        private void BeginBlock(ulong number)
        {
            _inFlightBlock = number;
            _allocBefore = GC.GetTotalAllocatedBytes(true);
            _gen0Before = GC.CollectionCount(0);
            _gen1Before = GC.CollectionCount(1);
            _gen2Before = GC.CollectionCount(2);
            _blockClock.Restart();
        }

        private void EndBlock(ulong number, BlockHeader header, double wallMs)
        {
            var allocated = GC.GetTotalAllocatedBytes(true) - _allocBefore;
            _process.Refresh();
            _timings.TryRemove(number, out var timing);
            _stats.TryRemove(number, out var stats);
            var position = _sinceBoundary + 1;
            _sinceBoundary = _cadence.IsBoundary(number) ? 0 : position;
            _blocks.Add(new ReplayBlockRecord
            {
                Number = number,
                GasUsed = timing?.Gas ?? (long)header.GasUsed,
                TxCount = (int)(timing?.TxCount ?? 0),
                WallMs = wallMs,
                WindowPosition = position,
                AllocBytes = allocated,
                Gen0 = GC.CollectionCount(0) - _gen0Before,
                Gen1 = GC.CollectionCount(1) - _gen1Before,
                Gen2 = GC.CollectionCount(2) - _gen2Before,
                RssBytes = _process.WorkingSet64,
                ExecMs = timing?.ExecMs ?? 0,
                RootMs = timing?.RootMs ?? 0,
                PersistMs = timing?.PersistMs ?? 0,
                AccountReads = timing?.AccountReads ?? 0,
                StorageReads = timing?.StorageReads ?? 0,
                ExecutionMs = stats?.ExecutionMs ?? 0,
                StateReadMs = stats?.StateReadMs ?? 0,
                StateHashMs = stats?.StateHashMs ?? 0,
                CommitMs = stats?.CommitMs ?? 0,
            });
            if (_sinceBoundary == 0) SnapshotCache("window", number);
        }

        private void Capture<TState>(LogLevel level, TState state, string message)
        {
            if (state is not IReadOnlyList<KeyValuePair<string, object?>> fields) return;
            var format = Field(fields, "{OriginalFormat}") as string;
            if (format == null) return;

            if (format.StartsWith(TimingEvent, StringComparison.Ordinal))
            {
                var number = ulong.Parse(Convert.ToString(Field(fields, "BlockNumber"), CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
                _timings[number] = new TimingEntry(
                    Long(fields, "Gas"), Long(fields, "TxCount"), Double(fields, "ExecMs"), Double(fields, "RootMs"),
                    Double(fields, "PersistMs"), Long(fields, "AcctReads"), Long(fields, "SlotReads"));
                return;
            }

            if (format.StartsWith(StatsEvent, StringComparison.Ordinal))
            {
                var number = ulong.Parse(Convert.ToString(Field(fields, "BlockNumber"), CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
                _stats[number] = new StatsEntry(
                    Double(fields, "ExecutionMs"),
                    Double(fields, "AccountReadsMs") + Double(fields, "StorageReadsMs") + Double(fields, "CodeReadsMs"),
                    Double(fields, "AccountHashesMs") + Double(fields, "AccountUpdatesMs") + Double(fields, "StorageUpdatesMs"),
                    Double(fields, "AccountCommitsMs") + Double(fields, "StorageCommitsMs") + Double(fields, "DbCommitMs") + Double(fields, "BlockWriteMs"));
                return;
            }

            if (level >= LogLevel.Warning && _diagnostics.Count < MaxDiagnostics)
                _diagnostics.Enqueue($"{level} {message}");

            if (level >= LogLevel.Error && format.StartsWith(RejectionEvent, StringComparison.Ordinal) && RejectedBlock == null)
            {
                RejectedBlock = ulong.Parse(Convert.ToString(Field(fields, "Block"), CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
                RejectionReason = message;
                _onRejected(RejectedBlock.Value, message);
            }
        }

        private static object? Field(IReadOnlyList<KeyValuePair<string, object?>> fields, string name)
        {
            for (var i = 0; i < fields.Count; i++)
                if (fields[i].Key == name) return fields[i].Value;
            return null;
        }

        private static long Long(IReadOnlyList<KeyValuePair<string, object?>> fields, string name)
            => Convert.ToInt64(Field(fields, name), CultureInfo.InvariantCulture);

        private static double Double(IReadOnlyList<KeyValuePair<string, object?>> fields, string name)
            => Convert.ToDouble(Field(fields, name), CultureInfo.InvariantCulture);

        private sealed record StatsEntry(double ExecutionMs, double StateReadMs, double StateHashMs, double CommitMs);

        private sealed record TimingEntry(
            long Gas, long TxCount, double ExecMs, double RootMs, double PersistMs, long AccountReads, long StorageReads);

        private sealed class CaptureLogger : ILogger
        {
            private readonly ReplayRecorder _owner;

            public CaptureLogger(ReplayRecorder owner) => _owner = owner;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => _owner.Capture(logLevel, state, formatter(state, exception));
        }
    }
}
