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
        public static SnapResumeMode DecideResumeMode(SnapPhase savedPhase, bool healTargetValid, bool pivotMoved)
        {
            if (savedPhase == SnapPhase.Phase3Running && healTargetValid) return SnapResumeMode.Phase3Heal;
            if (pivotMoved) return SnapResumeMode.Phase2;
            if (savedPhase == SnapPhase.Complete) return SnapResumeMode.ClearOrphan;
            if (savedPhase == SnapPhase.Phase2Running) return SnapResumeMode.Phase2;
            return SnapResumeMode.Fresh;
        }
        internal static bool PivotMoved(SnapSyncState savedState, BlockHeader pivot, byte[] pivotHash) =>
            savedState.PivotBlockNumber != (ulong)pivot.BlockNumber
            || !Nethereum.Util.ByteUtil.AreEqual(savedState.PivotBlockHash, pivotHash);
        private static (SnapSyncState ResumeFrom, bool SkipPhase2) RouteResume(
            IChainStoreBundle bundle, BlockHeader pivot, byte[] pivotHash, ILogger logger)
        {
            var savedState = bundle.Metadata.GetSnapSyncState();
            SnapSyncState resumeFrom = null;
            bool skipPhase2 = false;
            if (savedState != null && savedState.SchemaVersion == SnapSyncStateRlpEncoder.CurrentSchemaVersion)
            {
                bool pivotMoved = PivotMoved(savedState, pivot, pivotHash);
                bool healTargetValid = savedState.HealTargetRoot != null && savedState.HealTargetRoot.Length == 32;
                switch (DecideResumeMode(savedState.Phase, healTargetValid, pivotMoved))
                {
                    case SnapResumeMode.Phase3Heal:
                        logger.LogInformation(
                            "snap.bootstrap.resume phase=Phase3 saved_pivot={Saved} new_pivot={New} heal_target=0x{Root}",
                            savedState.PivotBlockNumber, pivot.BlockNumber, savedState.HealTargetRoot.ToHex());
                        resumeFrom = savedState;
                        skipPhase2 = true;
                        break;
                    case SnapResumeMode.Phase2:
                        if (pivotMoved)
                            logger.LogInformation(
                                "snap.bootstrap.pivot_moved saved={Saved} new={New} — resuming delta against new root",
                                savedState.PivotBlockNumber, pivot.BlockNumber);
                        else
                            logger.LogInformation(
                                "snap.bootstrap.resume phase=Phase2 pivot={Pivot} accounts_synced={Acc} bytes_synced={Bytes}",
                                savedState.PivotBlockNumber,
                                savedState.Counters?.AccountsSynced ?? 0,
                                savedState.Counters?.AccountBytes ?? 0);
                        resumeFrom = savedState;
                        break;
                    case SnapResumeMode.ClearOrphan:
                        logger.LogWarning(
                            "snap.bootstrap.state phase=Complete_orphan pivot={Pivot} — clearing and restarting",
                            savedState.PivotBlockNumber);
                        bundle.Metadata.ClearSnapSyncState();
                        break;
                    case SnapResumeMode.Fresh:
                        break;
                }
            }
            else if (savedState != null)
            {
                logger.LogWarning(
                    "snap.bootstrap.state schema_mismatch saved_version={Saved} expected={Expected} — treating as fresh",
                    savedState.SchemaVersion, SnapSyncStateRlpEncoder.CurrentSchemaVersion);
                bundle.Metadata.ClearSnapSyncState();
            }
            return (resumeFrom, skipPhase2);
        }
        public static bool ShouldMarkPhase2Entry(bool skipPhase2, bool backfillOnly)
            => !skipPhase2 && !backfillOnly;
        public static bool ShouldLogStalledRecycle(DateTimeOffset? lastLoggedAt, DateTimeOffset now, TimeSpan minInterval)
            => lastLoggedAt is null || now - lastLoggedAt.Value >= minInterval;
        private static void StampPhase2Entry(
            IChainStoreBundle bundle, BlockHeader pivot, byte[] pivotHash, SnapSyncState resumeFrom,
            bool skipPhase2, bool backfillOnly, SnapSyncMetrics metrics, ILogger logger)
        {
            if (ShouldMarkPhase2Entry(skipPhase2, backfillOnly))
            {
                var fromPhase = resumeFrom?.Phase ?? SnapPhase.NotStarted;
                logger.LogInformation(
                    "snap.phase.transition from={From} to={To} pivot={Pivot}",
                    fromPhase, SnapPhase.Phase2Running, pivot.BlockNumber);
                bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase2Running,
                    PivotBlockNumber = (ulong)pivot.BlockNumber,
                    PivotBlockHash = pivotHash,
                    HealTargetRoot = new byte[32],
                    Tasks = resumeFrom?.Tasks ?? System.Array.Empty<SnapSyncAccountTask>(),
                    Counters = resumeFrom?.Counters ?? SnapSyncCounters.Zero,
                });
                if (resumeFrom != null)
                {
                    metrics?.RecordResume(fromPhase);
                }
            }
            else if (resumeFrom != null)
            {
                metrics?.RecordResume(resumeFrom.Phase);
            }
        }
        public static SnapSyncState BuildHealEntryState(
            SnapSyncState persistedNow, SnapSyncState resumeFrom,
            ulong pivotBlock, byte[] pivotHash, byte[] healTarget)
            => new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = pivotBlock,
                PivotBlockHash = pivotHash,
                HealTargetRoot = healTarget,
                Tasks = persistedNow?.Tasks ?? resumeFrom?.Tasks ?? System.Array.Empty<SnapSyncAccountTask>(),
                Counters = persistedNow?.Counters ?? resumeFrom?.Counters ?? SnapSyncCounters.Zero,
            };
    }
}
