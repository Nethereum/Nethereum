using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Services
{
    public sealed class RewindCoordinator : IRewindCoordinator
    {
        private readonly IChainStoreBundle _bundle;

        public RewindCoordinator(IChainStoreBundle bundle)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        }

        public async Task<RewindResult> RewindToAsync(
            ulong targetBlock,
            RewindPolicy policy,
            CancellationToken ct = default)
        {
            await DrainPendingFlushAsync().ConfigureAwait(false);

            var currentHead = _bundle.Metadata.GetLastBlock();
            if (currentHead <= targetBlock)
            {
                return new RewindResult(
                    RewindOutcome.NoOp,
                    currentHead,
                    UndoneCount: 0UL,
                    RestoredCheckpoint: null,
                    Detail: $"current head {currentHead:N0} already at or below target {targetBlock:N0}");
            }

            RefuseRewindBelowPromotionFloor(targetBlock, currentHead);

            var pathKeyed = IsPathKeyedState();
            if (pathKeyed)
            {
                return await RewindPathKeyedAsync(targetBlock, currentHead, policy, ct).ConfigureAwait(false);
            }

            if (policy != RewindPolicy.SnapshotOnly && _bundle.JournalEnabled)
            {
                if (await JournalCoversAsync(targetBlock).ConfigureAwait(false))
                {
                    return await RewindByJournalThenSnapshotAsync(targetBlock, currentHead, policy, ct).ConfigureAwait(false);
                }
                if (policy == RewindPolicy.JournalOnly)
                    return NoPathAvailable(
                        currentHead, $"journal does not cover target {targetBlock:N0} and policy is JournalOnly");
            }
            else if (policy == RewindPolicy.JournalOnly)
            {
                return NoPathAvailable(currentHead, "journal not enabled and policy is JournalOnly");
            }

            return TrySnapshot(targetBlock, currentHead, journalError: null, ct);
        }

        private async Task DrainPendingFlushAsync()
        {
            if (_bundle is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
        }

        private void RefuseRewindBelowPromotionFloor(ulong targetBlock, ulong currentHead)
        {
            if (_bundle is IPromotionFloorGuard promotionFloorGuard)
            {
                var promotionFloor = promotionFloorGuard.PromotionFloor(currentHead);
                if (promotionFloor.HasValue && targetBlock < promotionFloor.Value)
                    throw new InvalidOperationException(
                        $"Rewind target {targetBlock:N0} is below the promotion floor {promotionFloor.Value:N0}: " +
                        "the target has already been promoted to append-only history and cannot be rewound to.");
            }
        }

        private bool IsPathKeyedState()
            => !ReferenceEquals(_bundle.StateTrieNodes, _bundle.TrieNodes);

        private void ClearFlatCacheAfterJournalRestore()
            => (_bundle.State as IFlatCacheInvalidatable)?.ClearCache();


        private async Task<RewindResult> RewindPathKeyedAsync(
            ulong targetBlock, ulong currentHead, RewindPolicy policy, CancellationToken ct)
        {
            if (policy == RewindPolicy.SnapshotOnly)
                return TrySnapshot(targetBlock, currentHead, journalError: null, ct);

            var result = await NodeHistoryThenSnapshotAsync(
                targetBlock, currentHead, journalError: null, ct).ConfigureAwait(false);
            if (policy == RewindPolicy.JournalOnly && result.Outcome == RewindOutcome.SnapshotUsed)
                return NoPathAvailable(
                    currentHead, $"node history does not cover target {targetBlock:N0} and policy is JournalOnly");
            return await FinalizeRewindAsync(result, currentHead, ct).ConfigureAwait(false);
        }

        private async Task<RewindResult> RewindByJournalThenSnapshotAsync(
            ulong targetBlock, ulong currentHead, RewindPolicy policy, CancellationToken ct)
        {
            try
            {
                var service = new StateRewindService(
                    _bundle.State, _bundle.Diffs, _bundle.Blocks, _bundle.Metadata);
                ulong undone;
                try
                {
                    undone = await service.RewindWithJournalAsync(targetBlock, ct).ConfigureAwait(false);
                }
                finally
                {
                    ClearFlatCacheAfterJournalRestore();
                }
                var newHead = _bundle.Metadata.GetLastBlock();
                var journalResult = new RewindResult(
                    RewindOutcome.JournalUsed, newHead, UndoneCount: undone, RestoredCheckpoint: null,
                    Detail: $"journal-rewound {undone:N0} block(s) to {newHead:N0}");
                return await FinalizeRewindAsync(journalResult, currentHead, ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (policy == RewindPolicy.JournalFirstThenSnapshot)
            {
                return TrySnapshot(targetBlock, currentHead, ex.Message, ct);
            }
        }

        private static RewindResult NoPathAvailable(ulong currentHead, string detail)
            => new RewindResult(
                RewindOutcome.NoPathAvailable, currentHead, UndoneCount: null, RestoredCheckpoint: null,
                Detail: detail);

        private async Task<RewindResult> NodeHistoryThenSnapshotAsync(
            ulong targetBlock, ulong currentHead, string journalError, CancellationToken ct)
        {
            var pathKeyed = IsPathKeyedState();
            if (pathKeyed && _bundle is INodeHistoryRecoverable recoverable)
            {
                ct.ThrowIfCancellationRequested();
                string replayError = null;
                try
                {
                    var committed = await recoverable
                        .RecoverToAsync(targetBlock, FlatRecoverySource.ReplayJournal, _ => { }, ct).ConfigureAwait(false);
                    return NodeHistoryResult(committed, "node-history + journal replay");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { replayError = ex.Message; }

                try
                {
                    var committed = await recoverable
                        .RecoverToAsync(targetBlock, FlatRecoverySource.ReconcileFromTrie, _ => { }, ct).ConfigureAwait(false);
                    return NodeHistoryResult(committed, "node-history + reconcile");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    var detail = $"node-history journal-replay failed ({replayError}); reconcile failed ({ex.Message})";
                    journalError = journalError is null ? detail : $"{journalError}; {detail}";
                }
            }

            return TrySnapshot(targetBlock, currentHead, journalError, ct);
        }

        private RewindResult NodeHistoryResult(ulong committed, string how)
        {
            var newHead = _bundle.Metadata.GetLastBlock();
            return new RewindResult(
                RewindOutcome.NodeHistoryUsed,
                newHead,
                UndoneCount: null,
                RestoredCheckpoint: null,
                Detail: $"{how} rebuilt committed state to {newHead:N0} (target {committed:N0})");
        }

        private async Task<RewindResult> FinalizeRewindAsync(RewindResult result, ulong originalHead, CancellationToken ct)
        {
            if ((result.Outcome != RewindOutcome.JournalUsed && result.Outcome != RewindOutcome.NodeHistoryUsed)
                || result.NewHead >= originalHead)
                return result;

            for (var n = originalHead; n > result.NewHead; n--)
            {
                ct.ThrowIfCancellationRequested();
                var block = (BigInteger)n;
                await _bundle.Logs.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.Receipts.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.Transactions.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.Uncles.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.Withdrawals.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.BlockAccessLists.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _bundle.Blocks.DeleteByNumberAsync(block).ConfigureAwait(false);
            }

            return result;
        }

        private async Task<bool> JournalCoversAsync(ulong targetBlock)
        {
            var newest = await _bundle.Diffs.GetNewestDiffBlockAsync().ConfigureAwait(false);
            if (!newest.HasValue) return false;
            var oldest = await _bundle.Diffs.GetOldestDiffBlockAsync().ConfigureAwait(false);
            if (!oldest.HasValue) return false;
            return oldest.Value <= (BigInteger)(targetBlock + 1);
        }

        private RewindResult TrySnapshot(
            ulong targetBlock, ulong currentHead, string journalError, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var cp = _bundle.Metadata.GetNearestCheckpointAtOrBefore(targetBlock);
            if (cp is null)
            {
                var detail = journalError is null
                    ? $"no checkpoint at or below {targetBlock:N0}"
                    : $"journal unusable ({journalError}); no checkpoint at or below {targetBlock:N0}";
                return NoPathAvailable(currentHead, detail);
            }

            var snapshotDir = _bundle.ResolveCheckpointSnapshotPath(cp.Value.BlockNumber);
            if (string.IsNullOrEmpty(snapshotDir) || !System.IO.Directory.Exists(snapshotDir))
            {
                return NoPathAvailable(
                    currentHead,
                    $"metadata checkpoint at {cp.Value.BlockNumber:N0} has no on-disk snapshot at {snapshotDir}");
            }

            return new RewindResult(
                RewindOutcome.SnapshotUsed,
                cp.Value.BlockNumber,
                UndoneCount: null,
                RestoredCheckpoint: cp,
                Detail: $"snapshot available at {snapshotDir} for block {cp.Value.BlockNumber:N0}");
        }
    }
}
