using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class StateBackpressureGate
    {
        private readonly SnapSyncClient _client;
        private long _stateBackpressurePausedSinceMs;

        public StateBackpressureGate(SnapSyncClient client)
        {
            _client = client;
        }

        public async Task WaitAsync(int consumerIdx, CancellationToken ct)
        {
            var backpressure = _client.StateWriteBackpressure;
            if (backpressure == null) return;

            string pressure; int pausePolls = 0;
            while ((pressure = backpressure()) != null)
            {
                ct.ThrowIfCancellationRequested();
                var pausedFor = MarkPausedAndPublishGauge();
                if (consumerIdx == 0 && pausePolls % 6 == 0)
                    LogPause(pausedFor, pressure);
                pausePolls++;
                await Task.Delay(_client.StateBackpressurePollMs, ct).ConfigureAwait(false);
            }

            var pausedSince = Interlocked.Exchange(ref _stateBackpressurePausedSinceMs, 0);
            if (pausedSince != 0 || pausePolls > 0)
                _client.Metrics?.SetPhase2BackpressurePausedFor(TimeSpan.Zero);
            if (pausedSince == 0) return;
            _client.Logger.LogInformation(
                "Phase 2 leaf stream: resumed after {PausedSec}s of storage backpressure",
                (long)ElapsedSince(pausedSince).TotalSeconds);
        }

        private TimeSpan MarkPausedAndPublishGauge()
        {
            var now = Environment.TickCount64;
            var since = Interlocked.CompareExchange(ref _stateBackpressurePausedSinceMs, now, 0);
            var pausedFor = since == 0 ? TimeSpan.Zero : ElapsedSince(since);
            _client.Metrics?.SetPhase2BackpressurePausedFor(pausedFor);
            return pausedFor;
        }

        private static TimeSpan ElapsedSince(long sinceMs)
            => TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64 - sinceMs));

        private void LogPause(TimeSpan pausedFor, string pressure)
        {
            if (pausedFor >= _client.StateBackpressureWarnAfter)
                _client.Logger.LogWarning(
                    "Phase 2 leaf stream: paused for {PausedSec}s — storage backpressure has not released for over {WarnAfterSec}s: {Pressure}. " +
                    "State download stays paused until the store's write valve releases; check the store's compaction and the pressure figures.",
                    (long)pausedFor.TotalSeconds, (long)_client.StateBackpressureWarnAfter.TotalSeconds, pressure);
            else
                _client.Logger.LogInformation(
                    "Phase 2 leaf stream: paused for {PausedSec}s — storage backpressure: {Pressure}",
                    (long)pausedFor.TotalSeconds, pressure);
        }
    }
}
