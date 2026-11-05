using System.Linq;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using PivotState = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.PivotState;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class ResumeRouter
    {
        internal static SnapResumeMode DecideResumeMode(SnapPhase savedPhase, bool healTargetValid, bool pivotMoved)
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

        internal static ResumePlan Route(
            IChainStoreBundle bundle, BlockHeader pivot, byte[] pivotHash, ILogger logger)
        {
            MigrateLegacyStorageDebts(bundle, logger);
            var savedState = bundle.Metadata.GetSnapSyncState();
            if (savedState == null)
                return ResumePlan.None;
            if (savedState.SchemaVersion != SnapSyncStateRlpEncoder.CurrentSchemaVersion)
            {
                ClearOnSchemaMismatch(bundle, savedState, logger);
                return ResumePlan.None;
            }

            bool pivotMoved = PivotMoved(savedState, pivot, pivotHash);
            bool healTargetValid = savedState.HealTargetRoot != null && savedState.HealTargetRoot.Length == 32;
            switch (DecideResumeMode(savedState.Phase, healTargetValid, pivotMoved))
            {
                case SnapResumeMode.Phase3Heal:
                    return ResumeAtPhase3(savedState, pivot, logger);
                case SnapResumeMode.Phase2:
                    return ResumeAtPhase2(savedState, pivot, pivotMoved, logger);
                case SnapResumeMode.ClearOrphan:
                    ClearOrphan(bundle, savedState, logger);
                    return ResumePlan.None;
                default:
                    return ResumePlan.None;
            }
        }

#pragma warning disable CS0618
        private static void MigrateLegacyStorageDebts(IChainStoreBundle bundle, ILogger logger)
        {
            var metadata = bundle.Metadata;
            if (metadata.CountOpenDeferredStorageDebts() == 0) return;

            var legacy = metadata.ListOpenDeferredStorageDebts()
                .Select(d => new SnapSyncClient.AccountNeedingHeal(d.AccountHash, d.DiscoveredStorageRoot))
                .ToList();
            var union = NeedsHealAccountsCodec.Union(metadata.GetDeferredHealAccountsBlob(), legacy);
            if (union.Count > 0)
                metadata.SaveDeferredHealAccountsBlob(NeedsHealAccountsCodec.Encode(union));
            metadata.ClearAllDeferredStorageDebts();
            logger.LogInformation(
                "snap.bootstrap.legacy_debts migrated={Count} — healed as needs-heal accounts at the live pivot", legacy.Count);
        }
#pragma warning restore CS0618

        private static ResumePlan ResumeAtPhase3(SnapSyncState savedState, BlockHeader pivot, ILogger logger)
        {
            logger.LogInformation(
                "snap.bootstrap.resume phase=Phase3 saved_pivot={Saved} new_pivot={New} heal_target=0x{Root}",
                savedState.PivotBlockNumber, pivot.BlockNumber, savedState.HealTargetRoot.ToHex());
            return new ResumePlan(savedState, true);
        }

        private static ResumePlan ResumeAtPhase2(
            SnapSyncState savedState, BlockHeader pivot, bool pivotMoved, ILogger logger)
        {
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
            return new ResumePlan(savedState, false);
        }

        private static void ClearOrphan(IChainStoreBundle bundle, SnapSyncState savedState, ILogger logger)
        {
            logger.LogWarning(
                "snap.bootstrap.state phase=Complete_orphan pivot={Pivot} — clearing and restarting",
                savedState.PivotBlockNumber);
            bundle.Metadata.ClearSnapSyncState();
        }

        private static void ClearOnSchemaMismatch(IChainStoreBundle bundle, SnapSyncState savedState, ILogger logger)
        {
            logger.LogWarning(
                "snap.bootstrap.state schema_mismatch saved_version={Saved} expected={Expected} — treating as fresh",
                savedState.SchemaVersion, SnapSyncStateRlpEncoder.CurrentSchemaVersion);
            bundle.Metadata.ClearSnapSyncState();
        }

        internal static SnapBootstrapper.Result SkipIfStateCommitted(IChainStoreBundle bundle, ILogger logger)
        {
            var existing = bundle.Metadata.GetLastBlock();
            if (existing > 0)
            {
                logger.LogInformation(
                    "snap.bootstrap.skip reason=committed_state block={Block}",
                    existing);
                return new SnapBootstrapper.Result { Ran = false, SkipReason = $"existing state at block {existing}" };
            }
            return null;
        }

        internal static bool ShouldMarkPhase2Entry(bool skipPhase2, bool backfillOnly)
            => !skipPhase2 && !backfillOnly;

        internal static void EnterPhase2(
            SnapRunContext context, BlockHeader pivot, byte[] pivotHash, ResumePlan plan, bool backfillOnly)
        {
            var resumeFrom = plan.ResumeFrom;
            var metrics = context.Metrics;
            if (ShouldMarkPhase2Entry(plan.SkipPhase2, backfillOnly))
            {
                var fromPhase = resumeFrom?.Phase ?? SnapPhase.NotStarted;
                context.Logger.LogInformation(
                    "snap.phase.transition from={From} to={To} pivot={Pivot}",
                    fromPhase, SnapPhase.Phase2Running, pivot.BlockNumber);
                (context.Bundle as IFlatStateReconciler)?.DiscardCertifyJournal();
                context.Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
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

        internal static SnapSyncState BuildHealEntryState(
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

        internal static void SaveHealEntryState(IChainStoreBundle bundle, SnapSyncState resumeFrom, PivotState healCyclePivot)
        {
            var persistedRecycle = bundle.Metadata.GetSnapSyncState();
            bundle.Metadata.SaveSnapSyncState(BuildHealEntryState(
                persistedRecycle, resumeFrom,
                (ulong)healCyclePivot.Header.BlockNumber, healCyclePivot.Hash, healCyclePivot.Header.StateRoot));
        }
    }
}
