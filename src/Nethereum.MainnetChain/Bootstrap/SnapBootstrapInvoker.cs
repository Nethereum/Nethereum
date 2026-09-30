using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.MainnetChain.Configuration;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.MainnetChain.Bootstrap
{
    public static class SnapBootstrapInvoker
    {
        public static async Task<SnapBootstrapper.Result> RunIfConfiguredAsync(
            IChainStoreBundle bundle,
            IPeerPool? pool,
            IFetchRequestScheduler? scheduler,
            MainnetChainServerConfig config,
            ILogger logger,
            CancellationToken ct,
            ICanonicalStateRootSource? canonicalTip = null,
            SnapSyncMetrics? metrics = null,
            Nethereum.CoreChain.Sync.HeaderFollowService? headerFollow = null)
        {
            if (!config.SnapBootstrap)
            {
                logger.LogInformation("snap.bootstrap.skip reason=config_disabled");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "SnapBootstrap config flag is false" };
            }

            (ulong From, ulong To)? headerSweepOverride =
                config.HeadersFrom.HasValue ? (config.HeadersFrom.Value, config.HeadersTo) : null;
            if (headerSweepOverride.HasValue)
                logger.LogWarning(
                    "snap.bootstrap header sweep OVERRIDE active: From={From} To={To} (cursor bypassed)",
                    headerSweepOverride.Value.From, headerSweepOverride.Value.To);

            try
            {
                var syncNode = new SyncNode(bundle, MainnetChainActivations.Instance, logger, pool, scheduler);
                return await syncNode.RunSnapBootstrapAsync(
                    canonicalTip,
                    new SnapSyncOrchestratorOptions
                    {
                        UseBackwardSkeleton = config.BackwardSkeletonPhase1,
                        Metrics = metrics,
                        HeaderSweepOverride = headerSweepOverride,
                        BackfillOnly = config.SnapPhase1Only,
                        RunHistoryBackfill = config.RunHistoryBackfillDuringStateSync,
                        Phase1First = config.SnapPhase1First,
                        AccountConcurrency = config.SnapAccountConcurrency,
                        LargeContractConcurrency = config.SnapLargeContractConcurrency,
                        FinalizeVerify = config.SnapFinalizeVerify,
                        EnableFlatReconcile = config.SnapEnableFlatReconcile,
                        HeaderFollow = headerFollow,
                    },
                    ct).ConfigureAwait(false);
            }
            finally
            {
                try { (bundle as IBulkDurabilityBoundary)?.CheckpointBulk(); }
                catch (System.Exception ex)
                {
                    logger.LogWarning(ex, "snap.bootstrap bulk window commit failed; resume re-fetches from the last checkpoint");
                }
            }
        }
    }
}
