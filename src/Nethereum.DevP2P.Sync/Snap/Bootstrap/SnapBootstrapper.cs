#pragma warning disable CS0618
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public enum SnapResumeMode
    {
        Fresh,
        Phase2,
        Phase3Heal,
        ClearOrphan,
    }

    public static class SnapBootstrapper
    {
        public sealed class Result
        {
            public bool Ran { get; init; }
            public string SkipReason { get; init; }
            public ulong PivotBlockNumber { get; init; }
            public byte[] PivotStateRoot { get; init; }
            public int AccountCount { get; init; }
            public int SlotCount { get; init; }
            public int BytecodeCount { get; init; }

            public Task HistoryBackfill { get; init; } = Task.CompletedTask;

            public Task StateCompaction { get; init; } = Task.CompletedTask;
        }

        public sealed record PivotState(BlockHeader Header, byte[] Hash);

        public sealed class RollingPivot
        {
            private PivotState _current;
            public RollingPivot(BlockHeader header, byte[] hash) => _current = new PivotState(header, hash);
            public PivotState Current => System.Threading.Volatile.Read(ref _current);
            public void Adopt(PivotState next)
            {
                while (true)
                {
                    var cur = System.Threading.Volatile.Read(ref _current);
                    if (next.Header.BlockNumber <= cur.Header.BlockNumber) return;
                    if (System.Threading.Interlocked.CompareExchange(ref _current, next, cur) == cur) return;
                }
            }
        }

        public sealed class SnapStorageCompletenessGateReport
        {
            public int PendingDeferredHealAccounts { get; set; }
            public bool DeferredHealBlobPresent { get; set; }
            [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
            public ulong OpenDeferredStorageDebts { get; set; }
            [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
            public ulong MalformedDeferredStorageDebtRows { get; set; }
            [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
            public int UnresolvedBigAccounts { get; set; }
            [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
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

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SnapBootstrapper.RunAsync — single-pivot cold-start")]
        public static async Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            SnapRunOptions? options = null,
            CancellationToken ct = default)
        {
            var context = SnapRunContext.Create(bundle, peer, pivot, pivotHash, logger, options);
            var skipped = ResumeRouter.SkipIfStateCommitted(context.Bundle, context.Logger);
            if (skipped != null) return skipped;

            context.Scheduler?.ClearStatelessMarks();
            if (!context.BackfillOnly && Snap2Run.ShouldBalHeal(context.Activations, context.Pivot, context.BalHealEnabled))
                return await Snap2Run.Create(context).RunAsync(ct).ConfigureAwait(false);
            return await Snap1Run.Create(context).RunAsync(ct).ConfigureAwait(false);
        }

        public static bool ShouldBalHeal(IChainActivations activations, BlockHeader header, bool balHealEnabled)
            => Snap2Run.ShouldBalHeal(activations, header, balHealEnabled);

        public static bool ShouldLogStalledRecycle(DateTimeOffset? lastLoggedAt, DateTimeOffset now, TimeSpan minInterval)
            => PivotTiming.ShouldLogStalled(lastLoggedAt, now, minInterval);

        public static readonly TimeSpan BackfillStopTimeout = ParallelBlockBackfiller.StageDrainTimeout + TimeSpan.FromSeconds(5);

        public static Task StopBackfillAsync(CancellationTokenSource backfillCts, Task backfill, ILogger logger)
            => Phase1History.StopAsync(backfillCts, backfill, logger);

        [Obsolete("No longer used: bytecode completeness is guaranteed by Phase-2 and heal code fetches and the certify sweep.")]
        public static Task EnsureBytecodeCompleteAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, byte[] stateRoot,
            ILogger logger, CancellationToken ct)
            => Task.CompletedTask;

        [Obsolete("No longer used: contract code that needs heal is fetched by the heal at the live pivot (Phase3Heal), and the certify sweep hands missing code to the same heal.")]
        public static Task FetchMissingBytecodeAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, SnapSyncState resumeFrom,
            byte[] pivotStateRoot, ILogger logger, CancellationToken ct,
            System.Collections.Generic.IReadOnlyList<byte[]> phase2DeferredCode = null)
            => Phase3Heal.HealCodeAsync(bundle, scheduler, pivotStateRoot, phase2DeferredCode, logger, ct);

        public static SnapStorageCompletenessGateReport BuildStorageCompletenessGateReport(IChainStoreBundle bundle)
            => StorageCompletenessGate.Build(bundle);

        public static Task ReconcileAndCertifyFlatAsync(
            IFlatStateReconciler flatReconciler, byte[] stateRoot, ulong pivotBlockNumber,
            ILogger logger, CancellationToken ct)
            => FlatCertifier.ReconcileAndCertifyAsync(flatReconciler, stateRoot, pivotBlockNumber, logger, ct);

        [Obsolete("No longer used: the flat state is certified by a single generation run at finalize.")]
        public static Task ReconcileAndCertifyFlatAsync(
            IFlatStateReconciler flatReconciler, byte[] stateRoot, ulong pivotBlockNumber, bool verify,
            ILogger logger, CancellationToken ct)
            => ReconcileAndCertifyFlatAsync(flatReconciler, stateRoot, pivotBlockNumber, logger, ct);

        [Obsolete("No longer used: the flat state is certified by a single generation run at finalize.")]
        public static Task<PivotState> ReconcileFlatStateAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, bool finalizeVerify, bool enableFlatReconcile, ILogger logger, CancellationToken ct)
            => ReconcileFlatStateAsync(bundle, scheduler, rollingPivot, pivotRefresher, metrics, logger, ct);

        public static Task<PivotState> ReconcileFlatStateAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            SnapSyncMetrics? metrics, ILogger logger, CancellationToken ct)
            {
            var pivots = new PivotWatch(rollingPivot, pivotRefresher, scheduler, logger);
            return new FlatCertifier(bundle, scheduler, pivots, new Phase3Heal(bundle, scheduler, pivots, null, metrics, logger), logger)
                .RunAsync(null, ct);
        }

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static bool HasDeferredStorageHealWork(
            IChainMetadataStore metadata,
            System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> discoveredAccounts)
            => discoveredAccounts is { Count: > 0 };

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static Task ResolveOpenDebtFinalRootsWithRetargetAsync(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            ILogger logger,
            CancellationToken ct)
            => Task.CompletedTask;

        public static System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> ResolveDeferredHealAccounts(
            System.Collections.Generic.IReadOnlyList<SnapSyncClient.AccountNeedingHeal> freshFromException,
            byte[] persistedBlob,
            ILogger logger = null)
            => Phase3Heal.ResolveAccounts(freshFromException, persistedBlob, logger);

        public static Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>> BuildHealerPivotRefresher(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            RollingPivot rollingPivot)
            => new PivotWatch(
                rollingPivot, pivotRefresher, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
                .AsHealerRefresher();

        public static Func<CancellationToken, Task<byte[]>> BuildClientPivotRefresher(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            RollingPivot rollingPivot,
            ILogger logger)
            => new PivotWatch(rollingPivot, pivotRefresher, null, logger).AsClientRefresher();

        public static Task<byte[]> AnchorFreshPivotAtStartAsync(
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            RollingPivot rollingPivot,
            BlockHeader bootPivot,
            ILogger logger,
            CancellationToken ct)
            => new PivotWatch(rollingPivot, pivotRefresher, null, logger).AnchorFreshAtStartAsync(bootPivot, ct);

        public static Task<ParallelBlockBackfiller.BackfillResult> RunPhase1BackfillWithStallRetryAsync(
            Func<CancellationToken, Task<ParallelBlockBackfiller.BackfillResult>> attempt,
            ILogger logger,
            CancellationToken ct,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
            => Phase1Backfill.RunWithStallRetryAsync(attempt, logger, ct, delay);

        public static SnapResumeMode DecideResumeMode(SnapPhase savedPhase, bool healTargetValid, bool pivotMoved)
            => ResumeRouter.DecideResumeMode(savedPhase, healTargetValid, pivotMoved);

        public static bool PivotMoved(SnapSyncState savedState, BlockHeader pivot, byte[] pivotHash)
            => ResumeRouter.PivotMoved(savedState, pivot, pivotHash);

        public static bool ShouldMarkPhase2Entry(bool skipPhase2, bool backfillOnly)
            => ResumeRouter.ShouldMarkPhase2Entry(skipPhase2, backfillOnly);

        public static SnapSyncState BuildHealEntryState(
            SnapSyncState persistedNow, SnapSyncState resumeFrom,
            ulong pivotBlock, byte[] pivotHash, byte[] healTarget)
            => ResumeRouter.BuildHealEntryState(persistedNow, resumeFrom, pivotBlock, pivotHash, healTarget);
    }
}
