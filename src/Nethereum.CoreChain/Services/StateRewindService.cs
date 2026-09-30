using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Services
{
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

        public async Task<ulong> RewindWithJournalAsync(ulong targetBlock, CancellationToken ct = default)
        {
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

                foreach (var entry in diff.StorageDiffs)
                {
                    var pre = entry.PreValue ?? Array.Empty<byte>();
                    await _stateStore.SaveStorageByKeccakAsync(entry.Address, entry.SlotKey, pre).ConfigureAwait(false);
                }

                _metadataStore.Commit(n - 1, prevHash);
                if (_metadataStore.GetLastFetchedHeader() > n - 1)
                    _metadataStore.SetLastFetchedHeader(n - 1);
                if (_metadataStore.GetLastFetchedBody() > n - 1)
                    _metadataStore.SetLastFetchedBody(n - 1);

                undone++;
            }

            await _diffStore.DeleteDiffsAboveBlockAsync((BigInteger)targetBlock).ConfigureAwait(false);

            _metadataStore.DeleteCheckpointsAbove(targetBlock);

            return undone;
        }
    }
}
