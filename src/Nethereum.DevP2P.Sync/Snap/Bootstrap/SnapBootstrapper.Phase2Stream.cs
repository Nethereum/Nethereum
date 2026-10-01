using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static partial class SnapBootstrapper
    {
        public static Func<CancellationToken, Task<byte[]>> BuildClientPivotRefresher(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            RollingPivot rollingPivot,
            ILogger logger)
        {
            return async refreshCt =>
            {
                var fresh = await pivotRefresher(false, refreshCt).ConfigureAwait(false);
                if (fresh.HasValue)
                {
                    var rotated = new PivotState(fresh.Value.Header, fresh.Value.Hash);
                    rollingPivot.Adopt(rotated);
                    logger.LogInformation(
                        "snap.pivot.target block={Block} root=0x{Root}",
                        rotated.Header.BlockNumber, rotated.Header.StateRoot.ToHex());
                }
                return rollingPivot.Current.Header.StateRoot;
            };
        }
        public static async Task<byte[]> AnchorFreshPivotAtStartAsync(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            RollingPivot rollingPivot,
            BlockHeader bootPivot,
            ILogger logger,
            CancellationToken ct)
        {
            if (pivotRefresher == null)
                return bootPivot.StateRoot;

            var fresh = await pivotRefresher(true, ct).ConfigureAwait(false);
            if (fresh.HasValue)
                rollingPivot.Adopt(new PivotState(fresh.Value.Header, fresh.Value.Hash));

            var anchored = rollingPivot.Current.Header;
            logger.LogInformation(
                "snap.pivot.phase2_start block={Block} root=0x{Root}",
                anchored.BlockNumber, anchored.StateRoot.ToHex());
            return anchored.StateRoot;
        }

        private static async Task<(SnapSyncClient.SyncResult SyncResult, bool HealPhaseEntered)> RunPhase2OrHealAsync(
            SnapSyncClient client,
            BlockHeader pivot,
            SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink,
            bool skipPhase2,
            TrieSnapSyncSink sink,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            IFetchRequestScheduler scheduler,
            IChainStoreBundle bundle,
            SnapSyncMetrics metrics,
            ILogger logger,
            CancellationToken ct)
        {
            try
            {
                var streamed = await RunPhase2StreamAsync(
                        client, pivot, resumeFrom, checkpointSink, skipPhase2, sink,
                        rollingPivot, pivotRefresher, scheduler, bundle, metrics, logger, ct)
                    .ConfigureAwait(false);
                return (streamed, false);
            }
            catch (SnapSyncClient.SnapRootMismatchException ex) when (scheduler != null)
            {
                var healed = await RunHealPhaseAsync(
                        ex, bundle, scheduler, resumeFrom, skipPhase2, sink,
                        rollingPivot, pivotRefresher, metrics, logger, ct)
                    .ConfigureAwait(false);
                return (healed, true);
            }
        }

        private static async Task<SnapSyncClient.SyncResult> RunPhase2StreamAsync(
            SnapSyncClient client,
            BlockHeader pivot,
            SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink,
            bool skipPhase2,
            TrieSnapSyncSink sink,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            IFetchRequestScheduler? scheduler,
            IChainStoreBundle bundle,
            SnapSyncMetrics? metrics,
            ILogger logger,
            CancellationToken ct)
        {
            SnapSyncClient.SyncResult syncResult = null!;
            if (skipPhase2)
            {
                syncResult = new SnapSyncClient.SyncResult
                {
                    Sink = sink,
                    ComputedRoot = resumeFrom!.HealTargetRoot,
                    RootMatchesTarget = false,
                    AccountCount = sink.AccountCount,
                    FinalTargetRoot = resumeFrom.HealTargetRoot,
                    CodeHashesNeedingHeal = DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob()),
                };
                throw new SnapSyncClient.SnapRootMismatchException(
                    $"resume Phase3 — driving heal against persisted target 0x{resumeFrom.HealTargetRoot.ToHex()} (sentinel)",
                    System.Array.Empty<SnapSyncClient.AccountNeedingHeal>(),
                    DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob()));
            }
            if (pivotRefresher != null)
            {
                client.PivotRefresher = BuildClientPivotRefresher(pivotRefresher, rollingPivot, logger);
            }

            var targetRoot = await AnchorFreshPivotAtStartAsync(
                pivotRefresher, rollingPivot, pivot, logger, ct).ConfigureAwait(false);

            syncResult = await client.SyncStateWithCheckpointAsync(
                targetRoot, resumeFrom, checkpointSink, ct).ConfigureAwait(false);

            PersistDeferredHealCode(bundle.Metadata, syncResult.CodeHashesNeedingHeal);

            if (scheduler != null)
            {
                await HealDeferredStorageDebtsAsync(
                        bundle,
                        scheduler,
                        syncResult.AccountsNeedingHeal,
                        rollingPivot,
                        pivotRefresher,
                        metrics,
                        logger,
                        ct,
                        "matched-root")
                    .ConfigureAwait(false);
            }
            return syncResult;
        }
    }
}
