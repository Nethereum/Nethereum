using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Services
{
    /// <summary>
    /// Journal-based state rewind. Reads the per-block reverse diffs recorded
    /// by <see cref="HistoricalStateStore"/> via <see cref="IStateDiffStore"/>,
    /// replays them in reverse order from <c>currentBlock</c> down to
    /// <c>target+1</c>, and updates each store atomically per call.
    ///
    /// <para>
    /// Ordering per block (matters for crash recovery):
    /// </para>
    /// <list type="number">
    ///   <item>Restore pre-values via the state store (each Save/Delete is
    ///     atomic per-call).</item>
    ///   <item>Flip the canonical-head cursor down to <c>n-1</c> via
    ///     <see cref="IChainMetadataStore.Commit"/> (atomic per-store).</item>
    ///   <item>Delete the consumed diff via
    ///     <see cref="IStateDiffStore.DeleteBlockDiffAsync"/>.</item>
    /// </list>
    ///
    /// <para>
    /// Crash semantics:
    /// </para>
    /// <list type="bullet">
    ///   <item>Crash mid-step 1 → cursor still at n; diff still present;
    ///     restart re-restores pre-values (idempotent).</item>
    ///   <item>Crash between step 2 and step 3 → cursor at n-1; diff for n
    ///     still present (orphan). The next forward execution at block n
    ///     overwrites the diff. Harmless.</item>
    ///   <item>Crash after step 3 → fully rewound.</item>
    /// </list>
    /// </summary>
    public sealed class StateRewindService
    {
        private readonly IStateStore _stateStore;
        private readonly IStateDiffStore _diffStore;
        private readonly IBlockStore _blockStore;
        private readonly IChainMetadataStore _metadataStore;

        public StateRewindService(
            IStateStore stateStore,
            IStateDiffStore diffStore,
            IBlockStore blockStore,
            IChainMetadataStore metadataStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _diffStore = diffStore ?? throw new ArgumentNullException(nameof(diffStore));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        }

        /// <summary>
        /// Roll the chain back to <paramref name="targetBlock"/> by replaying
        /// the journal in reverse. Returns the number of blocks undone.
        /// Throws if a block in the range has no recorded diff (journal gap).
        /// Idempotent: if the current block is already at or below
        /// <paramref name="targetBlock"/>, returns 0.
        /// </summary>
        public async Task<ulong> RewindWithJournalAsync(ulong targetBlock, CancellationToken ct = default)
        {
            // Start from the NEWEST RECORDED DIFF, not the cursor. After a
            // state-root mismatch the cursor is at N-1 (never advanced past
            // the divergent block), but the journal flush already persisted
            // block N's diff to IStateDiffStore — and block N's state writes
            // are sitting in the state CFs from PersistExecutionStateChangesAsync.
            // Rewinding only from cursor would leave block N's state writes
            // orphaned. Rewinding from newest-diff cleans them up.
            var newestDiff = await _diffStore.GetNewestDiffBlockAsync().ConfigureAwait(false);
            ulong start = newestDiff.HasValue
                ? (ulong)newestDiff.Value
                : _metadataStore.GetLastBlock();
            if (start <= targetBlock) return 0UL;

            ulong undone = 0;
            for (ulong n = start; n > targetBlock; n--)
            {
                ct.ThrowIfCancellationRequested();

                var diff = await _diffStore.GetBlockDiffAsync((BigInteger)n).ConfigureAwait(false);
                if (diff == null)
                {
                    throw new InvalidOperationException(
                        $"Journal-rewind aborted at block {n:N0}: no reverse-diff recorded. " +
                        $"Either the journal was pruned below {n:N0}, or the block was synced " +
                        $"before journal-on-write was wired. Use --rewind-to-checkpoint for " +
                        $"the snapshot-based path or rebuild state from genesis with --re-execute-from.");
                }

                var prevHash = await _blockStore.GetHashByNumberAsync((BigInteger)(n - 1)).ConfigureAwait(false);
                if (prevHash == null || prevHash.Length != 32)
                {
                    throw new InvalidOperationException(
                        $"Journal-rewind aborted at block {n:N0}: missing block hash for " +
                        $"target {(n - 1):N0} in IBlockStore. The header data must be present " +
                        $"for the rewind to advance the canonical-head cursor.");
                }

                // 1. Restore account pre-values. Null pre-value means the
                //    account didn't exist before this block; delete it.
                foreach (var entry in diff.AccountDiffs)
                {
                    if (entry.PreValue == null)
                    {
                        await _stateStore.DeleteAccountAsync(entry.Address).ConfigureAwait(false);
                    }
                    else
                    {
                        await _stateStore.SaveAccountAsync(entry.Address, entry.PreValue).ConfigureAwait(false);
                    }
                }

                // 2. Restore storage pre-values. Empty pre-value means the
                //    slot was zero before this block; write empty (the
                //    backend treats it as "delete the slot").
                foreach (var entry in diff.StorageDiffs)
                {
                    var pre = entry.PreValue ?? Array.Empty<byte>();
                    await _stateStore.SaveStorageAsync(entry.Address, entry.Slot, pre).ConfigureAwait(false);
                }

                // 3. Flip the canonical-head cursor down to n-1, and clamp
                //    the pipeline cursors if they were above. Each is atomic
                //    per-store; the metadata Commit writes both fields in
                //    one WriteBatch internally.
                _metadataStore.Commit(n - 1, prevHash);
                if (_metadataStore.GetLastFetchedHeader() > n - 1)
                    _metadataStore.SetLastFetchedHeader(n - 1);
                if (_metadataStore.GetLastFetchedBody() > n - 1)
                    _metadataStore.SetLastFetchedBody(n - 1);

                // 4. Delete the consumed diff. A crash here leaves an orphan
                //    diff for n that the next forward execution overwrites.
                await _diffStore.DeleteBlockDiffAsync((BigInteger)n).ConfigureAwait(false);

                undone++;
            }

            // 5. Drop checkpoint metadata rows above the rewind target. They
            //    reference state roots from the now-reverted fork; leaving
            //    them would let GetNearestCheckpointAtOrBefore return a
            //    stale-fork snapshot on a future rewind attempt.
            _metadataStore.DeleteCheckpointsAbove(targetBlock);

            return undone;
        }
    }
}
