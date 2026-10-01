using System;
using System.Linq;
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
        public sealed class SnapStorageCompletenessGateReport
        {
            public int PendingDeferredHealAccounts { get; set; }
            public bool DeferredHealBlobPresent { get; set; }
            public ulong OpenDeferredStorageDebts { get; set; }
            public ulong MalformedDeferredStorageDebtRows { get; set; }
            public int UnresolvedBigAccounts { get; set; }
            public int FinalRootStorageNotDeepComplete { get; set; }
            public int DamagedStorageInventories { get; set; }

            public bool CanFinalize
                => !DeferredHealBlobPresent
                   && OpenDeferredStorageDebts == 0
                   && UnresolvedBigAccounts == 0
                   && FinalRootStorageNotDeepComplete == 0
                   && DamagedStorageInventories == 0;

            public override string ToString()
                => $"deferred_blob={(DeferredHealBlobPresent ? PendingDeferredHealAccounts.ToString() : "absent")} " +
                   $"open_debts={OpenDeferredStorageDebts} malformed_debts={MalformedDeferredStorageDebtRows} " +
                   $"unresolved_big={UnresolvedBigAccounts} final_not_deep={FinalRootStorageNotDeepComplete} " +
                   $"damaged={DamagedStorageInventories}";
        }
        public static SnapStorageCompletenessGateReport BuildStorageCompletenessGateReport(IChainStoreBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var report = new SnapStorageCompletenessGateReport();
            var blob = bundle.Metadata.GetDeferredHealAccountsBlob();
            if (blob != null)
            {
                report.DeferredHealBlobPresent = true;
                report.PendingDeferredHealAccounts = DeferredHealAccountsCodec.Decode(blob).Count;
            }

            var debts = bundle.Metadata.ListOpenDeferredStorageDebts();
            var countedOpenDebts = bundle.Metadata.CountOpenDeferredStorageDebts();
            report.OpenDeferredStorageDebts = countedOpenDebts;
            if (countedOpenDebts > (ulong)debts.Count)
                report.MalformedDeferredStorageDebtRows = countedOpenDebts - (ulong)debts.Count;
            foreach (var debt in debts)
            {
                switch (debt.Status)
                {
                    case StorageCompleteness.DeferredBigAccount:
                    case StorageCompleteness.DeferredUnavailable:
                        report.UnresolvedBigAccounts++;
                        break;
                    case StorageCompleteness.FinalRootReResolved:
                    case StorageCompleteness.DeepHealInProgress:
                        report.FinalRootStorageNotDeepComplete++;
                        break;
                    case StorageCompleteness.Damaged:
                        report.DamagedStorageInventories++;
                        break;
                }
            }

            if (bundle is IFlatStateReconciler damageStore)
                report.DamagedStorageInventories += damageStore.GetPersistedDamage().Count;

            return report;
        }
        private static void ThrowIfStorageCompletenessGateFails(IChainStoreBundle bundle)
        {
            var report = BuildStorageCompletenessGateReport(bundle);
            if (report.CanFinalize) return;

            throw new InvalidOperationException(
                "Snap-sync storage completeness gate failed before flat reconcile; refusing to finalize while storage debt remains: " +
                report);
        }
        public static async Task ReconcileAndCertifyFlatAsync(
            IFlatStateReconciler flatReconciler, byte[] stateRoot, ulong pivotBlockNumber, bool verify,
            ILogger logger, CancellationToken ct)
        {
            logger.LogInformation(
                "snap.flat.reconcile starting root=0x{Root} pivot={Block}", stateRoot.ToHex(), pivotBlockNumber);
            var reconcileSw = System.Diagnostics.Stopwatch.StartNew();
            var reconcile = await flatReconciler.ReconcileFlatStateAsync(
                    stateRoot,
                    msg => logger.LogInformation("snap.flat.reconcile {Progress}", msg),
                    ct)
                .ConfigureAwait(false);
            logger.LogInformation(
                "snap.flat.reconcile done in {Elapsed}: accounts={Accounts} slots={Slots} " +
                "ghost_accounts={GhostAccounts} ghost_slots={GhostSlots} added={AddedAccounts}/{AddedSlots} patched={PatchedAccounts}/{PatchedSlots}",
                reconcileSw.Elapsed, reconcile.AccountsScanned, reconcile.SlotsScanned,
                reconcile.GhostAccountsDeleted, reconcile.GhostSlotsDeleted,
                reconcile.AccountsAdded, reconcile.SlotsAdded,
                reconcile.AccountsPatched, reconcile.SlotsPatched);

            if (!verify)
            {
                logger.LogWarning(
                    "snap.flat.verify SKIPPED (SnapFinalizeVerify=false): trusting reconcile writes — {Repairs} repair(s) " +
                    "applied, no independent zero-diff re-read before go-live.", reconcile.TotalRepairs);
                return;
            }

            var verifySw = System.Diagnostics.Stopwatch.StartNew();
            var verifyResult = await flatReconciler.VerifyFlatStateAsync(
                    stateRoot,
                    msg => logger.LogInformation("snap.flat.verify {Progress}", msg),
                    ct)
                .ConfigureAwait(false);
            if (verifyResult.TotalRepairs != 0)
                throw new InvalidOperationException(
                    $"snap.flat.verify FAILED: {verifyResult.TotalRepairs} diffs remain after reconcile " +
                    $"(ghosts={verifyResult.GhostAccountsDeleted}/{verifyResult.GhostSlotsDeleted} " +
                    $"added={verifyResult.AccountsAdded}/{verifyResult.SlotsAdded} " +
                    $"patched={verifyResult.AccountsPatched}/{verifyResult.SlotsPatched}) — " +
                    "refusing to finalize on flat state that does not match the healed trie.");
            logger.LogInformation(
                "snap.flat.verify PASSED in {Elapsed}: flat state ghost-free — accounts={Accounts} slots={Slots} diffs=0",
                verifySw.Elapsed, verifyResult.AccountsScanned, verifyResult.SlotsScanned);
        }
        private static async Task<PivotState> ReconcileFlatStateAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, bool finalizeVerify, bool enableFlatReconcile, ILogger logger, CancellationToken ct)
        {
            var finalPivot = rollingPivot.Current;
            var pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;

            finalPivot = await RepairPendingFlatDamageAsync(
                    bundle, scheduler, rollingPivot, pivotRefresher, metrics, logger, finalPivot,
                    pivotBlockNumber, ct)
                .ConfigureAwait(false);
            pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;

            ThrowIfStorageCompletenessGateFails(bundle);

            if (!enableFlatReconcile)
            {
                logger.LogInformation(
                    "snap.flat.reconcile SKIPPED (EnableFlatReconcile=false): trusting inline flat writes from " +
                    "Phase 2 and heal — no full-trie ghost-cleanup sweep before go-live.");
                return finalPivot;
            }

            return await ReconcileWithUnresolvableNodeRepairAsync(
                    bundle, scheduler, rollingPivot, pivotRefresher, metrics, finalizeVerify, logger,
                    finalPivot, pivotBlockNumber, ct)
                .ConfigureAwait(false);
        }

        private static async Task<PivotState> RepairPendingFlatDamageAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, ILogger logger, PivotState finalPivot,
            ulong pivotBlockNumber, CancellationToken ct)
        {
            if (bundle is IFlatStateReconciler damageStore && scheduler != null)
            {
                var pending = damageStore.GetPersistedDamage();
                if (pending.Count > 0)
                {
                    logger.LogWarning(
                        "snap.flat.repair pending inventory of {Count} damaged subtree(s) persisted by a prior sweep — repairing before any new sweep",
                        pending.Count);
                    var currentTarget = finalPivot.Header.StateRoot;
                    var currentBlock = pivotBlockNumber;
                    var currentSeeds = new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>(pending);
                    TrieHealer.HealResult pendingResult = default;
                    for (int attempt = 1; ; attempt++)
                    {
                        var pendingHealer = CreateHealer(bundle, scheduler, logger, metrics);
                        if (pivotRefresher != null)
                            pendingHealer.PivotRefresher = BuildHealerPivotRefresher(pivotRefresher, rollingPivot);

                        pendingResult = await pendingHealer
                            .HealAsync(currentTarget, currentSeeds,
                                pivotBlock: currentBlock, wipeSeedsFirst: attempt == 1, ct: ct)
                            .ConfigureAwait(false);
                        if (pendingResult.Matched) break;

                        if (pendingResult.NeedsRetarget && pendingResult.RetargetSeeds is { Count: 0 })
                        {
                            pendingResult = pendingResult with { Matched = true };
                            break;
                        }

                        var previousTarget = currentTarget;
                        if (pendingResult.NeedsRetarget && pendingResult.RetargetRoot is { Length: 32 })
                        {
                            currentTarget = pendingResult.RetargetRoot;
                            currentBlock = pendingResult.RetargetBlock;
                            currentSeeds = new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>(pendingResult.RetargetSeeds ?? currentSeeds);
                        }
                        else if (pivotRefresher != null)
                        {
                            var advanced = await pivotRefresher(true, ct).ConfigureAwait(false);
                            if (advanced.HasValue)
                            {
                                rollingPivot.Adopt(new PivotState(advanced.Value.Header, advanced.Value.Hash));
                                currentTarget = rollingPivot.Current.Header.StateRoot;
                                currentBlock = (ulong)rollingPivot.Current.Header.BlockNumber;
                            }
                        }

                        bool advancedRoot = !Nethereum.Util.ByteUtil.AreEqual(currentTarget, previousTarget);
                        if (advancedRoot)
                            scheduler?.OnTargetRootChanged();
                        else if (pendingResult.TotalNodesFetched == 0)
                        {
                            metrics?.RecordPhase3RecycleNoProgress();
                            await Task.Delay(RecycleNoProgressBackoff, ct).ConfigureAwait(false);
                        }

                        logger.LogWarning(
                            "snap.flat.repair.pending.recycle attempt={Attempt} not converged ({Nodes} nodes banked; " +
                            "needs_retarget={NeedsRetarget}) — re-running against 0x{Root} ({Seeds} seed(s)), no re-wipe",
                            attempt, pendingResult.TotalNodesFetched, pendingResult.NeedsRetarget,
                            currentTarget.ToHex(), currentSeeds.Count);
                    }

                    damageStore.ClearPersistedDamage();
                    finalPivot = rollingPivot.Current;
                    pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;
                    logger.LogInformation(
                        "snap.flat.repair pending inventory repaired ({Nodes} nodes); sweeping to certify",
                        pendingResult.TotalNodesFetched);
                }
            }

            return finalPivot;
        }

        private static async Task<PivotState> ReconcileWithUnresolvableNodeRepairAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, bool finalizeVerify, ILogger logger, PivotState finalPivot,
            ulong pivotBlockNumber, CancellationToken ct)
        {
            for (int repairAttempt = 1; ; repairAttempt++)
            {
                try
                {
                if (bundle is IFlatStateReconciler flatReconciler)
                {
                    await ReconcileAndCertifyFlatAsync(
                            flatReconciler, finalPivot.Header.StateRoot, pivotBlockNumber, finalizeVerify, logger, ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning(
                        "snap.flat.reconcile SKIPPED: bundle {BundleType} does not expose IFlatStateReconciler — " +
                        "flat rows may diverge from the healed trie (rolling-pivot ghosts) and execution reads flat.",
                        bundle.GetType().Name);
                }
                    break;
                }
                catch (FlatReconcileUnresolvableNodeException hole) when
                    (hole.Subtrees.Count > 0 && scheduler != null && repairAttempt < 16)
                {
                    bool wholeTrieDamaged = hole.Subtrees.Any(s =>
                        Nethereum.Util.ByteUtil.AreEqual(s.AccountHash, FlatReconcileUnresolvableNodeException.WholeAccountTrieSentinel));
                    logger.LogWarning(
                        "snap.flat.repair attempt={Attempt}: {Count} damaged subtree(s) whole_trie={WholeTrie} — {Message}",
                        repairAttempt, hole.Subtrees.Count, wholeTrieDamaged, hole.Message);
                    var repairSeed = wholeTrieDamaged
                        ? new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>()
                        : new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>(hole.Subtrees);
                    var repairHealer = CreateHealer(bundle, scheduler, logger, metrics);
                    if (pivotRefresher != null)
                        repairHealer.PivotRefresher = BuildHealerPivotRefresher(pivotRefresher, rollingPivot);
                    TrieHealer.HealResult repairResult = default;
                    for (int healTry = 1; healTry <= 3; healTry++)
                    {
                        repairResult = await repairHealer
                            .HealAsync(finalPivot.Header.StateRoot, repairSeed,
                                pivotBlock: pivotBlockNumber, wipeSeedsFirst: healTry == 1, ct: ct)
                            .ConfigureAwait(false);
                        if (repairResult.Matched) break;
                        if (repairResult.NeedsRetarget && repairResult.RetargetSeeds != null)
                        {
                            if (repairResult.RetargetSeeds.Count == 0)
                            {
                                repairResult = repairResult with { Matched = true };
                                break;
                            }
                            repairSeed = new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>(repairResult.RetargetSeeds);
                        }
                        logger.LogWarning(
                            "snap.flat.repair seeded heal try {Try}/3 did not converge ({Nodes} nodes banked) — re-running in place",
                            healTry, repairResult.TotalNodesFetched);
                        finalPivot = rollingPivot.Current;
                        pivotBlockNumber = repairResult.NeedsRetarget && repairResult.RetargetRoot is { Length: 32 }
                            ? repairResult.RetargetBlock
                            : (ulong)finalPivot.Header.BlockNumber;
                    }
                    if (!repairResult.Matched)
                        throw new InvalidOperationException(
                            $"snap.flat.repair: seeded heal did not converge for the unresolvable subtree " +
                            $"(attempt {repairAttempt}) — surfacing to bootstrap retry.", hole);
                    if (bundle is IFlatStateReconciler repairedStore) repairedStore.ClearPersistedDamage();
                    finalPivot = rollingPivot.Current;
                    pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;
                    logger.LogInformation(
                        "snap.flat.repair attempt={Attempt} healed {Nodes} nodes for {Count} subtree(s); re-running reconcile at pivot {Pivot}",
                        repairAttempt, repairResult.TotalNodesFetched, repairSeed.Count, pivotBlockNumber);
                }
            }
            return finalPivot;
        }
    }
}
