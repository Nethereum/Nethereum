using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain
{
    public sealed class BlockExecutionStats
    {
        public TimeSpan AccountReads { get; internal set; }
        public TimeSpan StorageReads { get; internal set; }
        public TimeSpan CodeReads { get; internal set; }

        public int AccountLoaded { get; internal set; }
        public int StorageLoaded { get; internal set; }
        public int CodeLoaded { get; internal set; }
        public int CodeLoadBytes { get; internal set; }

        public TimeSpan AccountUpdates { get; internal set; }
        public TimeSpan AccountHashes { get; internal set; }
        public TimeSpan AccountCommits { get; internal set; }
        public TimeSpan StorageUpdates { get; internal set; }
        public TimeSpan StorageCommits { get; internal set; }

        public int AccountUpdated { get; internal set; }
        public int AccountDeleted { get; internal set; }
        public int StorageUpdated { get; internal set; }
        public int StorageDeleted { get; internal set; }

        public int CodeUpdated { get; internal set; }
        public int CodeUpdateBytes { get; internal set; }

        public IReadOnlyDictionary<int, int> PrecompileCalls { get; internal set; } = new Dictionary<int, int>();

        public string PrecompileCallsKey
        {
            get
            {
                var key = new StringBuilder();
                foreach (var call in PrecompileCalls)
                {
                    if (key.Length > 0) key.Append(',');
                    key.Append(call.Key.ToString("x2")).Append(':').Append(call.Value);
                }
                return key.ToString();
            }
        }

        public TimeSpan Execution { get; internal set; }
        public TimeSpan Validation { get; internal set; }
        public TimeSpan DatabaseCommit { get; internal set; }
        public TimeSpan BlockWrite { get; internal set; }
        public TimeSpan TotalTime { get; internal set; }
        public double MgasPerSecond { get; internal set; }

        public int TransactionCount { get; internal set; }
        public long GasUsed { get; internal set; }

        internal void SetTotal(TimeSpan total)
        {
            TotalTime = total;
            MgasPerSecond = total.Ticks <= 0 ? 0 : GasUsed / 1_000_000.0 / total.TotalSeconds;
        }
    }

    public sealed class StateRootPhaseTimings
    {
        public TimeSpan StorageUpdates { get; internal set; }
        public TimeSpan AccountUpdates { get; internal set; }
        public TimeSpan AccountHashes { get; internal set; }
        public TimeSpan StorageCommits { get; internal set; }
        public TimeSpan AccountCommits { get; internal set; }
        public TimeSpan DatabaseCommit { get; internal set; }
        public int AccountUpdated { get; internal set; }
        public int AccountDeleted { get; internal set; }
        public int StorageUpdated { get; internal set; }
        public int StorageDeleted { get; internal set; }
    }

    public interface IStateRootPhaseReporter
    {
        StateRootPhaseTimings TakePhaseTimings();
    }
}

namespace Nethereum.CoreChain
{
    internal sealed class PrecompileCallCounter : IPrecompileCallObserver
    {
        private const int AddressSpace = 0x101;
        private readonly int[] _calls = new int[AddressSpace];

        public void OnCall(int address)
        {
            if ((uint)address < AddressSpace) Interlocked.Increment(ref _calls[address]);
        }

        public IReadOnlyDictionary<int, int> Snapshot()
        {
            var calls = new SortedDictionary<int, int>();
            for (var address = 0; address < AddressSpace; address++)
            {
                var count = Volatile.Read(ref _calls[address]);
                if (count != 0) calls[address] = count;
            }
            return calls;
        }
    }

    internal sealed class BlockStatsCollector
    {
        private readonly PrecompileCallCounter _precompileCalls = new PrecompileCallCounter();
        private readonly StateReadMeter _meter;
        private readonly IStateReadMeterHost _host;
        private readonly long _blockStart;
        private long _executionStart;
        private long _executionEnd;
        private long _validationEnd;

        private BlockStatsCollector(IStateReadMeterHost host, StateReadMeter meter)
        {
            _host = host;
            _meter = meter;
            _blockStart = Stopwatch.GetTimestamp();
        }

        public static BlockStatsCollector TryAttach(IStateStore store)
        {
            if (store is not IStateReadMeterHost host) return null;
            var meter = new StateReadMeter();
            return host.TryAttachReadMeter(meter) ? new BlockStatsCollector(host, meter) : null;
        }

        public void Detach() => _host.DetachReadMeter(_meter);

        public HardforkConfig ObservePrecompiles(HardforkConfig config) =>
            config.Precompiles == null
                ? config
                : config.WithPrecompiles(config.Precompiles.WithCallObserver(_precompileCalls));

        public void BeginExecution(IIncrementalStateRootCalculator calculator)
        {
            (calculator as IStateRootPhaseReporter)?.TakePhaseTimings();
            _executionStart = Stopwatch.GetTimestamp();
        }

        public void EndExecution()
        {
            _executionEnd = Stopwatch.GetTimestamp();
            Detach();
        }

        public void EndValidation() => _validationEnd = Stopwatch.GetTimestamp();

        public BlockExecutionStats Complete(IIncrementalStateRootCalculator calculator, int transactionCount, long gasUsed)
        {
            var phases = (calculator as IStateRootPhaseReporter)?.TakePhaseTimings() ?? new StateRootPhaseTimings();
            var reads = _meter.AccountReads + _meter.StorageReads + _meter.CodeReads;
            var trie = phases.AccountHashes + phases.AccountUpdates + phases.StorageUpdates;
            var stats = new BlockExecutionStats
            {
                AccountReads = _meter.AccountReads,
                StorageReads = _meter.StorageReads,
                CodeReads = _meter.CodeReads,
                AccountLoaded = _meter.AccountLoaded,
                StorageLoaded = _meter.StorageLoaded,
                CodeLoaded = _meter.CodeLoaded,
                CodeLoadBytes = _meter.CodeLoadBytes,
                AccountUpdates = phases.AccountUpdates,
                AccountHashes = phases.AccountHashes,
                AccountCommits = phases.AccountCommits,
                StorageUpdates = phases.StorageUpdates,
                StorageCommits = phases.StorageCommits,
                AccountUpdated = phases.AccountUpdated,
                AccountDeleted = phases.AccountDeleted,
                StorageUpdated = phases.StorageUpdated,
                StorageDeleted = phases.StorageDeleted,
                CodeUpdated = _meter.CodeUpdated,
                CodeUpdateBytes = _meter.CodeUpdateBytes,
                PrecompileCalls = _precompileCalls.Snapshot(),
                Execution = NonNegative(Elapsed(_executionStart, _executionEnd) - reads),
                Validation = NonNegative(Elapsed(_executionEnd, _validationEnd) - trie),
                DatabaseCommit = phases.DatabaseCommit,
                TransactionCount = transactionCount,
                GasUsed = gasUsed
            };
            stats.SetTotal(Elapsed(_blockStart, Stopwatch.GetTimestamp()));
            return stats;
        }

        private static TimeSpan Elapsed(long start, long end)
            => TimeSpan.FromTicks((long)((end - start) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));

        private static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }
}
