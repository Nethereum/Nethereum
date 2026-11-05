using System;
using Nethereum.CoreChain.Sync;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public sealed class SnapSyncOrchestratorOptions
    {
        public bool UseBackwardSkeleton { get; init; } = true;

        public SnapSyncMetrics? Metrics { get; init; }

        public int RootRefreshIntervalMs { get; init; } = (int)PivotTiming.PollInterval.TotalMilliseconds;

        public ulong PivotStaleDistanceBlocks { get; init; } = SnapSyncOrchestrator.PivotStaleDistanceBlocks;

        public (ulong From, ulong To)? HeaderSweepOverride { get; init; }

        public bool BackfillOnly { get; init; }

        public bool RunHistoryBackfill { get; init; } = true;

        public bool Phase1First { get; init; }

        public int? AccountConcurrency { get; init; }

        public int? LargeContractConcurrency { get; init; }

        [Obsolete("No longer used: the flat state is always certified; the value is ignored.")]
        public bool EnableFlatReconcile { get; init; }

        [Obsolete("No longer used: the flat state is always certified; the value is ignored.")]
        public bool FinalizeVerify { get; init; } = true;

        public HeaderFollowService? HeaderFollow { get; init; }

        public bool BalHealEnabled { get; init; }
    }
}
