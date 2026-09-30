using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeSnapBootstrapOptionsBuilder
    {
        public static SnapSyncOrchestratorOptions Build(
            ChainNodeSyncConfig sync,
            SnapSyncMetrics? metrics = null,
            HeaderFollowService? headerFollow = null)
        {
            var snap = sync.Snap;
            (ulong From, ulong To)? headerSweepOverride =
                sync.HeadersFrom.HasValue ? (sync.HeadersFrom.Value, sync.HeadersTo) : null;

            return new SnapSyncOrchestratorOptions
            {
                UseBackwardSkeleton = snap.BackwardSkeletonPhase1,
                Metrics = metrics,
                HeaderSweepOverride = headerSweepOverride,
                BackfillOnly = snap.Phase1Only,
                Phase1First = snap.Phase1First,
                HeaderFollow = headerFollow,
            };
        }
    }
}
