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
        private static readonly TimeSpan RecycleWarnAfter = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RecycleErrorAfter = TimeSpan.FromHours(1);
        private static readonly TimeSpan RecycleNoProgressBackoff = TimeSpan.FromSeconds(2);
        public static bool HasDeferredStorageHealWork(
            IChainMetadataStore metadata,
            System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> discoveredAccounts)
        {
            if (discoveredAccounts is { Count: > 0 }) return true;
            return metadata != null && metadata.CountOpenDeferredStorageDebts() > 0;
        }
        private static async Task HealDeferredStorageDebtsAsync(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> discoveredAccounts,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics,
            ILogger logger,
            CancellationToken ct,
            string context)
        {
            if (!HasDeferredStorageHealWork(bundle.Metadata, discoveredAccounts)) return;

            var discoveredCount = discoveredAccounts?.Count ?? 0;
            if (discoveredAccounts is { Count: > 0 })
            {
                bundle.Metadata.SaveDeferredHealAccountsBlob(DeferredHealAccountsCodec.Encode(discoveredAccounts));
                DeferredStorageDebtFinalRootResolver.EnsureDebtRowsForDeferredHealAccounts(
                    bundle.Metadata,
                    discoveredAccounts,
                    rollingPivot.Current.Header.StateRoot,
                    rollingPivot.Current.Header.BlockNumber >= 0 ? (ulong)rollingPivot.Current.Header.BlockNumber : null);
            }

            var openBeforeResolve = bundle.Metadata.CountOpenDeferredStorageDebts();

            await ResolveOpenDebtFinalRootsWithRetargetAsync(
                    bundle, scheduler, rollingPivot, pivotRefresher, logger, ct)
                .ConfigureAwait(false);

            var seed = DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata);
            logger.LogInformation(
                "snap.phase3.needheal context={Context} discovered={Discovered} open_debts={OpenDebts} final_seeds={Seeds} - healing deferred storage subtrees",
                context, discoveredCount, openBeforeResolve, seed.Count);
            if (seed.Count == 0)
            {
                bundle.Metadata.ClearDeferredHealAccountsBlob();
                return;
            }

            await RunSeededStorageHealCyclesAsync(
                    seed, bundle, scheduler, rollingPivot, pivotRefresher, metrics, logger, ct)
                .ConfigureAwait(false);

            DeferredStorageDebtFinalRootResolver.MarkFinalStorageSeedsDeepComplete(bundle.Metadata, seed);
            bundle.Metadata.ClearDeferredHealAccountsBlob();
        }
        private const int DeferredResolveRetargetMaxAttempts = 8;
        public static async Task ResolveOpenDebtFinalRootsWithRetargetAsync(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            ILogger logger,
            CancellationToken ct)
        {
            for (int attempt = 1; attempt <= DeferredResolveRetargetMaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var current = rollingPivot.Current;

                await DeferredStorageDebtFinalRootResolver.ResolveAsync(
                        current.Header.StateRoot, (ulong)current.Header.BlockNumber,
                        bundle.Metadata, scheduler, logger, ct)
                    .ConfigureAwait(false);

                var stillUnresolved = CountOpenDebtsNotResolvedAt(bundle.Metadata, current.Header.StateRoot);
                if (stillUnresolved == 0)
                    return;

                var previousRoot = current.Header.StateRoot;
                if (pivotRefresher != null)
                {
                    var advanced = await pivotRefresher(true, ct).ConfigureAwait(false);
                    if (advanced.HasValue)
                        rollingPivot.Adopt(new PivotState(advanced.Value.Header, advanced.Value.Hash));
                }

                if (Nethereum.Util.ByteUtil.AreEqual(rollingPivot.Current.Header.StateRoot, previousRoot))
                {
                    logger.LogWarning(
                        "snap.phase3.deferred_resolve.exhausted attempt={Attempt} pivot could not advance past 0x{Root} " +
                        "(unresolved={Unresolved}) — resolved debts are healed; unresolved debts stay open and fail the storage-completeness gate if they remain",
                        attempt, previousRoot.ToHex(), stillUnresolved);
                    return;
                }

                scheduler?.OnTargetRootChanged();
                logger.LogWarning(
                    "snap.phase3.deferred_resolve.retarget attempt={Attempt} rolling deferred final-root resolution from 0x{Old} to 0x{New} " +
                    "(unresolved={Unresolved})",
                    attempt, previousRoot.ToHex(), rollingPivot.Current.Header.StateRoot.ToHex(), stillUnresolved);
            }

            logger.LogWarning(
                "snap.phase3.deferred_resolve.retarget_capped exhausted {Max} re-target attempts — resolved debts are healed; unresolved debts stay open and fail the storage-completeness gate if they remain",
                DeferredResolveRetargetMaxAttempts);
        }
        private static int CountOpenDebtsNotResolvedAt(IChainMetadataStore metadata, byte[] stateRoot)
        {
            var count = 0;
            foreach (var debt in metadata.ListOpenDeferredStorageDebts())
                if (debt.Status != StorageCompleteness.FinalRootReResolved
                    || !Nethereum.Util.ByteUtil.AreEqual(debt.FinalStateRoot, stateRoot))
                    count++;
            return count;
        }
        private static async Task RunSeededStorageHealCyclesAsync(
            System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seed,
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, ILogger logger, CancellationToken ct)
        {
            var currentTarget = rollingPivot.Current.Header.StateRoot;
            var currentBlock = (ulong)rollingPivot.Current.Header.BlockNumber;
            var currentSeed = seed;
            TrieHealer.HealResult deferredResult = default;
            for (int attempt = 1; ; attempt++)
            {
                var deferredHealer = CreateHealer(bundle, scheduler, logger, metrics);
                if (pivotRefresher != null)
                    deferredHealer.PivotRefresher = BuildHealerPivotRefresher(pivotRefresher, rollingPivot);

                deferredResult = await deferredHealer.HealAsync(currentTarget, currentSeed, pivotBlock: currentBlock, ct: ct).ConfigureAwait(false);
                if (deferredResult.Matched) break;

                if (deferredResult.NeedsRetarget && deferredResult.RetargetSeeds is { Count: 0 })
                {
                    deferredResult = deferredResult with { Matched = true };
                    break;
                }

                var previousTarget = currentTarget;
                if (deferredResult.NeedsRetarget && deferredResult.RetargetRoot is { Length: 32 })
                {
                    currentTarget = deferredResult.RetargetRoot;
                    currentBlock = deferredResult.RetargetBlock;
                    currentSeed = deferredResult.RetargetSeeds ?? currentSeed;
                }
                else if (pivotRefresher != null)
                {
                    var advanced = await pivotRefresher(true, ct).ConfigureAwait(false);
                    if (advanced.HasValue)
                    {
                        rollingPivot.Adopt(new PivotState(advanced.Value.Header, advanced.Value.Hash));
                        currentTarget = rollingPivot.Current.Header.StateRoot;
                        currentBlock = (ulong)rollingPivot.Current.Header.BlockNumber;

                        await DeferredStorageDebtFinalRootResolver.ResolveAsync(
                                currentTarget, currentBlock, bundle.Metadata, scheduler, logger, ct)
                            .ConfigureAwait(false);
                        currentSeed = DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata);
                    }
                }

                bool advancedRoot = !Nethereum.Util.ByteUtil.AreEqual(currentTarget, previousTarget);
                if (advancedRoot)
                    scheduler?.OnTargetRootChanged();
                else if (deferredResult.TotalNodesFetched == 0)
                {
                    metrics?.RecordPhase3RecycleNoProgress();
                    await Task.Delay(RecycleNoProgressBackoff, ct).ConfigureAwait(false);
                }

                logger.LogWarning(
                    "snap.phase3.deferred_heal.recycle attempt={Attempt} not converged ({Nodes} nodes banked; " +
                    "needs_retarget={NeedsRetarget}) — re-running against 0x{Root} ({Seeds} seed(s))",
                    attempt, deferredResult.TotalNodesFetched, deferredResult.NeedsRetarget,
                    currentTarget.ToHex(), currentSeed.Count);
            }
        }
        public static System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> ResolveDeferredHealAccounts(
            System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> freshFromException,
            byte[] persistedBlob,
            ILogger logger = null)
        {
            if (freshFromException is { Count: > 0 }) return freshFromException;
            if (persistedBlob == null) return null;
            var decoded = DeferredHealAccountsCodec.Decode(persistedBlob);
            if (decoded.Count == 0)
            {
                logger?.LogWarning(
                    "snap.phase3.deferredheal.corrupt persisted deferred-heal-accounts blob ({Length} bytes) " +
                    "did not decode to any entries (not a multiple of the 64-byte entry size) — the deferred " +
                    "storage heal for this account set is being skipped this attempt due to metadata corruption.",
                    persistedBlob.Length);
            }
            return decoded;
        }
        public static Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>> BuildHealerPivotRefresher(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            RollingPivot rollingPivot)
        {
            return async (forceFresh, refreshCt) =>
            {
                var fresh = await pivotRefresher(forceFresh, refreshCt).ConfigureAwait(false);
                if (fresh.HasValue)
                {
                    var rotated = new PivotState(fresh.Value.Header, fresh.Value.Hash);
                    rollingPivot.Adopt(rotated);
                }
                var cur = rollingPivot.Current;
                return (cur.Header.StateRoot, (ulong)cur.Header.BlockNumber);
            };
        }
        private static async Task<TrieHealer.HealResult> RunContinuousHealCycleAsync(
            PivotState healPivotEntry, byte[] healTargetEntry, IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler, SnapSyncState resumeFrom,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            RollingPivot rollingPivot, SnapSyncMetrics? metrics, ILogger logger, CancellationToken ct)
        {
            TrieHealer.HealResult healResult = default;
            long cumulativeHealNodes = 0;
            var healCyclePivot = healPivotEntry;
            var healCycleTarget = healTargetEntry;
            var stallDetector = new ProgressStallDetector(RecycleWarnAfter);
            DateTimeOffset? lastStalledLogAt = null;
            for (int healCycle = 1; ; healCycle++)
            {
                var healer = CreateHealer(bundle, scheduler, logger, metrics);
                if (pivotRefresher != null)
                {
                    healer.PivotRefresher = BuildHealerPivotRefresher(pivotRefresher, rollingPivot);
                }

                healResult = await healer.HealAsync(
                    healCycleTarget, seedStorageHeal: null,
                    pivotBlock: (ulong)healCyclePivot.Header.BlockNumber, ct: ct).ConfigureAwait(false);
                cumulativeHealNodes += healResult.TotalNodesFetched;
                if (healResult.Matched)
                    break;

                var previousTarget = healCycleTarget;
                if (pivotRefresher != null)
                {
                    var advanced = await pivotRefresher(true, ct).ConfigureAwait(false);
                    if (advanced.HasValue)
                        rollingPivot.Adopt(
                            new PivotState(advanced.Value.Header, advanced.Value.Hash));
                }
                healCyclePivot = rollingPivot.Current;
                healCycleTarget = healCyclePivot.Header.StateRoot;

                bool advancedRoot = !Nethereum.Util.ByteUtil.AreEqual(healCycleTarget, previousTarget);
                if (advancedRoot)
                    scheduler?.OnTargetRootChanged();
                if (healResult.TotalNodesFetched == 0 && !advancedRoot)
                {
                    metrics?.RecordPhase3RecycleNoProgress();
                    await Task.Delay(RecycleNoProgressBackoff, ct).ConfigureAwait(false);
                }

                var now = DateTimeOffset.UtcNow;
                if (stallDetector.Observe((ulong)cumulativeHealNodes, now)
                    && ShouldLogStalledRecycle(lastStalledLogAt, now, RecycleWarnAfter))
                {
                    lastStalledLogAt = now;
                    var stalledFor = stallDetector.StalledFor(now);
                    if (stalledFor >= RecycleErrorAfter)
                        logger.LogError(
                            "snap.phase3.recycle.stalled cycle={Cycle} stalled_for={StalledFor} — heal has made " +
                            "no progress this long; peers may not be serving. Still retrying (never aborts).",
                            healCycle, stalledFor);
                    else
                        logger.LogWarning(
                            "snap.phase3.recycle.stalled cycle={Cycle} stalled_for={StalledFor} — heal has not " +
                            "progressed recently; still retrying.",
                            healCycle, stalledFor);
                }

                var persistedRecycle = bundle.Metadata.GetSnapSyncState();
                bundle.Metadata.SaveSnapSyncState(BuildHealEntryState(
                    persistedRecycle, resumeFrom,
                    (ulong)healCyclePivot.Header.BlockNumber, healCyclePivot.Hash, healCycleTarget));

                logger.LogWarning(
                    "snap.phase3.recycle cycle={Cycle} not converged (cumulative {Nodes} nodes; " +
                    "this cycle: pruned={Pruned} absent={Absent} stale={Stale}) — " +
                    "re-pivoting to block {Block} root 0x{New} and continuing heal (persisted nodes retained)",
                    healCycle, cumulativeHealNodes,
                    healResult.Pruned, healResult.FetchedAbsent, healResult.FetchedStale,
                    healCyclePivot.Header.BlockNumber, healCycleTarget.ToHex());
            }

            healResult = healResult with { TotalNodesFetched = (int)Math.Min(cumulativeHealNodes, int.MaxValue) };
            return healResult;
        }
        private static async Task<SnapSyncClient.SyncResult> RunHealPhaseAsync(
            SnapSyncClient.SnapRootMismatchException ex,
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            SnapSyncState resumeFrom,
            bool skipPhase2,
            TrieSnapSyncSink sink,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics,
            ILogger logger,
            CancellationToken ct)
        {
            logger.LogWarning(ex, "Snap leaf-stream root mismatch — entering heal phase");
            PersistDeferredHealCode(bundle.Metadata, ex.CodeHashesNeedingHeal);
            var healPivotEntry = rollingPivot.Current;
            var healTargetEntry = healPivotEntry.Header.StateRoot;
            var persistedNow = bundle.Metadata.GetSnapSyncState();
            bundle.Metadata.SaveSnapSyncState(BuildHealEntryState(
                persistedNow, resumeFrom,
                (ulong)healPivotEntry.Header.BlockNumber, healPivotEntry.Hash, healTargetEntry));
            logger.LogInformation(
                "snap.phase.transition from=Phase2 to=Phase3 pivot={Pivot} heal_target=0x{Root}",
                healPivotEntry.Header.BlockNumber, healTargetEntry.ToHex());

            var healResult = await RunContinuousHealCycleAsync(
                    healPivotEntry, healTargetEntry, bundle, scheduler, resumeFrom, pivotRefresher,
                    rollingPivot, metrics, logger, ct)
                .ConfigureAwait(false);


            var deferredHeal = ResolveDeferredHealAccounts(
                ex.AccountsNeedingHeal, bundle.Metadata.GetDeferredHealAccountsBlob(), logger);
            await HealDeferredStorageDebtsAsync(
                    bundle,
                    scheduler,
                    deferredHeal,
                    rollingPivot,
                    pivotRefresher,
                    metrics,
                    logger,
                    ct,
                    "post root-mismatch")
                .ConfigureAwait(false);

            const int MinPlausibleStateNodes = 100_000;
            if (sink.AccountCount == 0 && healResult.TotalNodesFetched < MinPlausibleStateNodes && !skipPhase2)
                throw new InvalidOperationException(
                    $"Snap-sync bogus-converge guard: heal claimed matched against root 0x{healResult.FinalTargetRoot.ToHex()} " +
                    $"but only {healResult.TotalNodesFetched} nodes fetched and leaf stream wrote 0 accounts. " +
                    $"Real mainnet state has ~250M nodes — this is a false positive (likely pivot rotated to a stale subtree). " +
                    $"Refusing to proceed.");

            return new SnapSyncClient.SyncResult
            {
                Sink = sink,
                ComputedRoot = healResult.ComputedRoot,
                RootMatchesTarget = true,
                AccountCount = sink.AccountCount,
                FinalTargetRoot = healResult.FinalTargetRoot,
                CodeHashesNeedingHeal = DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob()),
            };
        }
    }
}
