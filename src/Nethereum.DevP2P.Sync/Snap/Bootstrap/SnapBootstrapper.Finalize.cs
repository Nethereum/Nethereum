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
        private static async Task<BlockHeader> PersistPivotAndCheckpointAsync(
            IChainStoreBundle bundle, byte[] finalPivotHash, ulong pivotBlockNumber,
            bool backfillRan, bool healPhaseEntered, ILogger logger, CancellationToken ct)
        {
            var finalPivotHeader = await bundle.Blocks.GetByHashAsync(finalPivotHash).ConfigureAwait(false);
            if (finalPivotHeader == null
                || finalPivotHeader.ParentHash == null || finalPivotHeader.ParentHash.Length != 32)
                throw new InvalidOperationException(
                    $"Snap-sync: full pivot header for block {pivotBlockNumber} (0x{finalPivotHash.ToHex()}) is not in the store at commit time. " +
                    $"The backward header walker must lay + verify it before finalising — refusing to persist a partial pivot header.");
            var existingHeaderCursor = bundle.Metadata.GetLastFetchedHeader();
            var existingBodyCursor = bundle.Metadata.GetLastFetchedBody();

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
            bundle.Metadata.ClearSnapSyncState();
            logger.LogInformation(
                "snap.phase.transition from={From} to=Complete pivot={Block} root=0x{Root}",
                healPhaseEntered ? "Phase3" : "Phase2",
                finalPivotHeader.BlockNumber, finalPivotHeader.StateRoot.ToHex());

            (bundle.State as IFlatCacheInvalidatable)?.ClearCache();

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
            return finalPivotHeader;
        }
    }
}
