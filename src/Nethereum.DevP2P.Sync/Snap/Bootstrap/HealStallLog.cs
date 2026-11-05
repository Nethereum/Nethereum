using System;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class HealStallLog
    {
        private readonly ILogger _logger;
        private readonly ProgressStallDetector _detector = new ProgressStallDetector(PivotWatch.RecycleWarnAfter);
        private DateTimeOffset? _lastLoggedAt;

        public HealStallLog(ILogger logger) => _logger = logger;

        public void Observe(int healCycle, long cumulativeHealNodes)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_detector.Observe((ulong)cumulativeHealNodes, now) || !PivotTiming.ShouldLogStalled(_lastLoggedAt, now, PivotWatch.RecycleWarnAfter)) return;
            _lastLoggedAt = now;
            var stalledFor = _detector.StalledFor(now);
            if (stalledFor >= PivotWatch.RecycleErrorAfter)
                _logger.LogError(
                    "snap.phase3.recycle.stalled cycle={Cycle} stalled_for={StalledFor} — heal has made " +
                    "no progress this long; peers may not be serving. Still retrying (never aborts).",
                    healCycle, stalledFor);
            else
                _logger.LogWarning(
                    "snap.phase3.recycle.stalled cycle={Cycle} stalled_for={StalledFor} — heal has not " +
                    "progressed recently; still retrying.",
                    healCycle, stalledFor);
        }
    }
}
