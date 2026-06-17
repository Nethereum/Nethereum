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

            if (policy != RewindPolicy.SnapshotOnly && _bundle.JournalEnabled)
            {
                var journalCovers = await JournalCoversAsync(targetBlock).ConfigureAwait(false);
                if (journalCovers)
                {
                    try
                    {
                        var service = new StateRewindService(
                            _bundle.State, _bundle.Diffs, _bundle.Blocks, _bundle.Metadata);
                        var undone = await service.RewindWithJournalAsync(targetBlock, ct).ConfigureAwait(false);
                        var newHead = _bundle.Metadata.GetLastBlock();
                        return new RewindResult(
                            RewindOutcome.JournalUsed,
                            newHead,
                            UndoneCount: undone,
                            RestoredCheckpoint: null,
                            Detail: $"journal-rewound {undone:N0} block(s) to {newHead:N0}");
                    }
                    catch (InvalidOperationException ex) when (policy == RewindPolicy.JournalFirstThenSnapshot)
                    {
                        return TrySnapshot(targetBlock, currentHead, journalError: ex.Message, ct);
                    }
                }

                if (policy == RewindPolicy.JournalOnly)
                {
                    return new RewindResult(
                        RewindOutcome.NoPathAvailable,
                        currentHead,
                        UndoneCount: null,
                        RestoredCheckpoint: null,
                        Detail: $"journal does not cover target {targetBlock:N0} and policy is JournalOnly");
                }
            }
            else if (policy == RewindPolicy.JournalOnly)
            {
                return new RewindResult(
                    RewindOutcome.NoPathAvailable,
                    currentHead,
                    UndoneCount: null,
                    RestoredCheckpoint: null,
                    Detail: "journal not enabled and policy is JournalOnly");
            }

            return TrySnapshot(targetBlock, currentHead, journalError: null, ct);
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
                return new RewindResult(
                    RewindOutcome.NoPathAvailable,
                    currentHead,
                    UndoneCount: null,
                    RestoredCheckpoint: null,
                    Detail: detail);
            }

            var snapshotDir = _bundle.ResolveCheckpointSnapshotPath(cp.Value.BlockNumber);
            if (string.IsNullOrEmpty(snapshotDir) || !System.IO.Directory.Exists(snapshotDir))
            {
                return new RewindResult(
                    RewindOutcome.NoPathAvailable,
                    currentHead,
                    UndoneCount: null,
                    RestoredCheckpoint: null,
                    Detail: $"metadata checkpoint at {cp.Value.BlockNumber:N0} has no on-disk snapshot at {snapshotDir}");
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
