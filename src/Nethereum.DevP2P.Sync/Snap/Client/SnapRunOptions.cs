using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public sealed class SnapRunOptions
    {
        public IFetchRequestScheduler? Scheduler { get; init; }

        public Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? PivotRefresher { get; init; }

        public IChainActivations? Activations { get; init; }

        public IPeerPool? Pool { get; init; }

        public SnapSyncMetrics? Metrics { get; init; }

        public bool RunBackfill { get; init; } = true;

        public bool UseBackwardSkeleton { get; init; }

        public int RootRefreshIntervalMs { get; init; } = 12_000;

        public bool FinalizeVerify { get; init; } = true;

        public bool EnableFlatReconcile { get; init; }

        public (ulong From, ulong To)? HeaderSweepOverride { get; init; }

        public bool BackfillOnly { get; init; }

        public bool Phase1First { get; init; }

        public int? AccountConcurrency { get; init; }

        public int? LargeContractConcurrency { get; init; }

        public bool ExternalHeaderFollow { get; init; }

        public bool BalHealEnabled { get; init; }

        public SnapBootstrapper.RollingPivot? RollingPivot { get; init; }

        public IBlockAccessListPeerSource? BlockAccessListPeers { get; init; }

        public IBlockAccessListApplier? BlockAccessListApplier { get; init; }
    }
}
