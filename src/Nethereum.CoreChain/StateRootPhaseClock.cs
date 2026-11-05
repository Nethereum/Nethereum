using System;
using System.Diagnostics;
using System.Threading;

namespace Nethereum.CoreChain
{
    internal enum StateRootPhase
    {
        StorageUpdates,
        AccountUpdates,
        AccountHashes,
        StorageCommits,
        AccountCommits,
        DatabaseCommit
    }

    internal enum StateRootCounter
    {
        AccountUpdated,
        AccountDeleted,
        StorageUpdated,
        StorageDeleted
    }

    internal sealed class StateRootPhaseClock
    {
        private readonly long[] _ticks = new long[6];
        private readonly int[] _counts = new int[4];

        public PhaseScope Time(StateRootPhase phase)
            => new PhaseScope(this, phase, phase, Stopwatch.GetTimestamp(), 0);

        public PhaseScope TimeExcluding(StateRootPhase phase, StateRootPhase nested)
            => new PhaseScope(this, phase, nested, Stopwatch.GetTimestamp(), Interlocked.Read(ref _ticks[(int)nested]));

        public void Count(StateRootCounter counter, int amount = 1) => Interlocked.Add(ref _counts[(int)counter], amount);

        public StateRootPhaseTimings Take() => new StateRootPhaseTimings
        {
            StorageUpdates = TakeTime(StateRootPhase.StorageUpdates),
            AccountUpdates = TakeTime(StateRootPhase.AccountUpdates),
            AccountHashes = TakeTime(StateRootPhase.AccountHashes),
            StorageCommits = TakeTime(StateRootPhase.StorageCommits),
            AccountCommits = TakeTime(StateRootPhase.AccountCommits),
            DatabaseCommit = TakeTime(StateRootPhase.DatabaseCommit),
            AccountUpdated = TakeCount(StateRootCounter.AccountUpdated),
            AccountDeleted = TakeCount(StateRootCounter.AccountDeleted),
            StorageUpdated = TakeCount(StateRootCounter.StorageUpdated),
            StorageDeleted = TakeCount(StateRootCounter.StorageDeleted)
        };

        private TimeSpan TakeTime(StateRootPhase phase)
            => TimeSpan.FromTicks((long)(Interlocked.Exchange(ref _ticks[(int)phase], 0) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));

        private int TakeCount(StateRootCounter counter) => Interlocked.Exchange(ref _counts[(int)counter], 0);

        public readonly struct PhaseScope : IDisposable
        {
            private readonly StateRootPhaseClock _clock;
            private readonly StateRootPhase _phase;
            private readonly StateRootPhase _nested;
            private readonly long _start;
            private readonly long _nestedBefore;

            internal PhaseScope(StateRootPhaseClock clock, StateRootPhase phase, StateRootPhase nested, long start, long nestedBefore)
            {
                _clock = clock;
                _phase = phase;
                _nested = nested;
                _start = start;
                _nestedBefore = nestedBefore;
            }

            public void Dispose()
            {
                var elapsed = Stopwatch.GetTimestamp() - _start;
                if (_nested != _phase)
                    elapsed -= Interlocked.Read(ref _clock._ticks[(int)_nested]) - _nestedBefore;
                Interlocked.Add(ref _clock._ticks[(int)_phase], elapsed);
            }
        }
    }
}
