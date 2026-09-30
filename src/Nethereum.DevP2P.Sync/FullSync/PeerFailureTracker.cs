using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Nethereum.DevP2P.Sync.FullSync
{
    internal enum PeerFailureOutcome
    {
        Cooldown,
        Bench,
        Dispose
    }

    internal sealed class PeerFailureTracker
    {
        private readonly int _dropThreshold;
        private readonly long _cooldownTicks;
        private readonly long _benchTicks;
        private readonly Func<long> _nowTicks;

        private sealed class State
        {
            public int Consecutive;
            public long CooldownUntilTicks;
        }

        private readonly ConcurrentDictionary<Guid, State> _states = new();

        public PeerFailureTracker(int dropThreshold, TimeSpan cooldown, TimeSpan bench, Func<long> nowTicks = null)
        {
            _dropThreshold = dropThreshold;
            _cooldownTicks = (long)(cooldown.TotalSeconds * Stopwatch.Frequency);
            _benchTicks = (long)(bench.TotalSeconds * Stopwatch.Frequency);
            _nowTicks = nowTicks ?? Stopwatch.GetTimestamp;
        }

        public bool IsOnCooldown(Guid peerId)
            => _states.TryGetValue(peerId, out var s) && _nowTicks() < Volatile.Read(ref s.CooldownUntilTicks);

        public void RecordSuccess(Guid peerId) => _states.TryRemove(peerId, out _);

        public void Forget(Guid peerId) => _states.TryRemove(peerId, out _);

        public PeerFailureOutcome RecordFailure(Guid peerId, bool wasTimeout)
        {
            var s = _states.GetOrAdd(peerId, _ => new State());
            var n = Interlocked.Increment(ref s.Consecutive);

            var outcome = PeerFailureOutcome.Cooldown;
            var cooldownTicks = _cooldownTicks;

            if (n >= _dropThreshold)
            {
                outcome = wasTimeout ? PeerFailureOutcome.Bench : PeerFailureOutcome.Dispose;
                if (wasTimeout) cooldownTicks = _benchTicks;
            }

            Volatile.Write(ref s.CooldownUntilTicks, _nowTicks() + cooldownTicks);
            return outcome;
        }
    }
}
