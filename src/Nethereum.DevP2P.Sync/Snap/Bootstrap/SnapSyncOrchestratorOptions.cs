using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Metrics;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public sealed class SnapSyncOrchestratorOptions
    {
        public bool UseBackwardSkeleton { get; init; } = true;

        public SnapSyncMetrics? Metrics { get; init; }

        public int RootRefreshIntervalMs { get; init; } = 12_000;

        public ulong PivotStaleDistanceBlocks { get; init; } = SnapSyncOrchestrator.PivotStaleDistanceBlocks;

        public (ulong From, ulong To)? HeaderSweepOverride { get; init; }

        public bool BackfillOnly { get; init; }

        public bool RunHistoryBackfill { get; init; } = true;

        public bool Phase1First { get; init; }

        public int? AccountConcurrency { get; init; }

        public int? LargeContractConcurrency { get; init; }

        public bool EnableFlatReconcile { get; init; }

        public bool FinalizeVerify { get; init; } = true;

        public HeaderFollowService? HeaderFollow { get; init; }

        public bool BalHealEnabled { get; init; }
    }
}
