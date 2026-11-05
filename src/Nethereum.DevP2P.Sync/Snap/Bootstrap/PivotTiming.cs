using System;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class PivotTiming
    {
        internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(12);
        internal static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(30);
        internal const ulong StaleDistanceBlocks = 120;

        internal static bool ShouldLogStalled(DateTimeOffset? lastLoggedAt, DateTimeOffset now, TimeSpan minInterval)
            => lastLoggedAt is null || now - lastLoggedAt.Value >= minInterval;
    }
}
