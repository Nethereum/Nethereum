using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class PivotCommit
    {
        internal static async Task<BlockHeader> PersistAsync(
            IChainStoreBundle bundle, byte[] finalPivotHash, ulong pivotBlockNumber,
            bool backfillRan, bool phase3Entered, ILogger logger, CancellationToken ct)
        {
            var finalPivotHeader = await LoadFullPivotHeaderAsync(bundle, finalPivotHash, pivotBlockNumber).ConfigureAwait(false);
            await CommitPivotBatchAsync(bundle, finalPivotHeader, finalPivotHash, pivotBlockNumber, backfillRan, ct).ConfigureAwait(false);
            ClearSnapStateAndLog(bundle, finalPivotHeader, phase3Entered, logger);

            (bundle.State as IFlatCacheInvalidatable)?.ClearCache();

            await SaveCheckpointWithRetryAsync(bundle, finalPivotHeader, finalPivotHash, logger, ct).ConfigureAwait(false);
            return finalPivotHeader;
        }

        private static async Task<BlockHeader> LoadFullPivotHeaderAsync(
            IChainStoreBundle bundle, byte[] finalPivotHash, ulong pivotBlockNumber)
        {
            var finalPivotHeader = await bundle.Blocks.GetByHashAsync(finalPivotHash).ConfigureAwait(false);
            if (finalPivotHeader == null
                || finalPivotHeader.ParentHash == null || finalPivotHeader.ParentHash.Length != 32)
                throw new InvalidOperationException(
                    $"Snap-sync: full pivot header for block {pivotBlockNumber} (0x{finalPivotHash.ToHex()}) is not in the store at commit time. " +
                    $"The backward header walker must lay + verify it before finalising — refusing to persist a partial pivot header.");
            return finalPivotHeader;
        }

        private static async Task CommitPivotBatchAsync(
            IChainStoreBundle bundle, BlockHeader finalPivotHeader, byte[] finalPivotHash,
            ulong pivotBlockNumber, bool backfillRan, CancellationToken ct)
        {
            var existingHeaderCursor = bundle.Metadata.GetLastFetchedHeader();
            var existingBodyCursor = bundle.Metadata.GetLastFetchedBody();

            (bundle as IFlatStateReconciler)?.DiscardCertifyJournal();
            using (var batch = bundle.BeginBatch())
            {
                batch.PutHeader(finalPivotHeader, finalPivotHash);
                batch.Commit(pivotBlockNumber, finalPivotHash);
                if (existingHeaderCursor < pivotBlockNumber)
                    batch.SetLastFetchedHeader(pivotBlockNumber);
                if (!backfillRan && existingBodyCursor < pivotBlockNumber)
                    batch.SetLastFetchedBody(pivotBlockNumber);
                batch.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Complete,
                    PivotBlockNumber = pivotBlockNumber,
                    PivotBlockHash = finalPivotHash,
                    HealTargetRoot = finalPivotHeader.StateRoot,
                    Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                    Counters = SnapSyncCounters.Zero,
                });
                await batch.CommitAsync(ct).ConfigureAwait(false);
            }
        }

        private static void ClearSnapStateAndLog(
            IChainStoreBundle bundle, BlockHeader finalPivotHeader, bool phase3Entered, ILogger logger)
        {
            bundle.Metadata.ClearSnapSyncState();
            logger.LogInformation(
                "snap.phase.transition from={From} to=Complete pivot={Block} root=0x{Root}",
                phase3Entered ? "Phase3" : "Phase2",
                finalPivotHeader.BlockNumber, finalPivotHeader.StateRoot.ToHex());
        }

        private static async Task SaveCheckpointWithRetryAsync(
            IChainStoreBundle bundle, BlockHeader finalPivotHeader, byte[] finalPivotHash,
            ILogger logger, CancellationToken ct)
        {
            const int checkpointAttempts = 3;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await bundle.SaveCheckpointAsync(
                        (ulong)finalPivotHeader.BlockNumber, finalPivotHeader.StateRoot, finalPivotHash, ct).ConfigureAwait(false);
                    logger.LogInformation(
                        "Snap-bootstrap: pivot (heal-complete) checkpoint persisted at block {Block} (attempt {Attempt}).",
                        finalPivotHeader.BlockNumber, attempt);
                    break;
                }
                catch (Exception ex) when (attempt < checkpointAttempts && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex,
                        "Snap-bootstrap: pivot checkpoint save failed at block {Block} (attempt {Attempt}/{Max}); retrying.",
                        finalPivotHeader.BlockNumber, attempt, checkpointAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex,
                        "Snap-bootstrap: pivot checkpoint could NOT be persisted at block {Block} after {Max} attempts. " +
                        "Forward execution proceeds but has no heal-complete rewind floor until the first periodic " +
                        "checkpoint lands — investigate the data disk.",
                        finalPivotHeader.BlockNumber, checkpointAttempts);
                    break;
                }
            }
        }
    }
}
