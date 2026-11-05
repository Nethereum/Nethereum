using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public sealed class BlockCommit
    {
        private readonly IStateStore _stateStore;
        private readonly NodeCommitBlockContext? _nodeCommitBlockContext;
        private readonly IAtomicBlockFlush? _atomicFlush;
        private readonly IFlushCadence _flushCadence;
        private readonly ILogger? _logger;

        public BlockCommit(
            IStateStore stateStore,
            NodeCommitBlockContext? nodeCommitBlockContext,
            IAtomicBlockFlush? atomicFlush,
            IFlushCadence? flushCadence,
            ILogger? logger)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _nodeCommitBlockContext = nodeCommitBlockContext;
            _atomicFlush = atomicFlush;
            _flushCadence = flushCadence ?? new EveryBlockFlushCadence();
            _logger = logger;
            RefuseArmedBracketWithoutFlush(nodeCommitBlockContext, atomicFlush);
            RefuseWindowedCadenceOverNonHistoricalStore(_flushCadence, stateStore);
        }

        public bool HasAtomicFlush => _atomicFlush != null;

        public IHistoricalStateProvider? HistoryProvider => _stateStore as IHistoricalStateProvider;

        public void Arm(ulong blockNumber) => _nodeCommitBlockContext?.Arm(blockNumber);

        public void Clear() => _nodeCommitBlockContext?.Clear();

        public async Task FlushAndJournalMatchedBlockAsync(
            IHistoricalStateProvider? historyProvider, BlockHeader header, byte[] blockHash)
        {
            bool boundary = _flushCadence.IsBoundary((ulong)header.BlockNumber);
            if (_atomicFlush != null && boundary)
            {
                await FlushWindowAtBoundaryAsync(_atomicFlush, historyProvider, header, blockHash).ConfigureAwait(false);
            }
            await RecordOrClearBlockJournalAsync(historyProvider, boundary).ConfigureAwait(false);
        }

        public async Task DiscardAndRevertUnpersistedBlockAsync(IHistoricalStateProvider? historyProvider)
        {
            _atomicFlush?.DiscardCapturedBlock();
            if (historyProvider != null)
            {
                await historyProvider.RevertCurrentBlockAsync().ConfigureAwait(false);
            }
        }

        public Task DiscardCancelledBlockAsync(IHistoricalStateProvider? historyProvider)
        {
            if (IsEveryBlock(_flushCadence)) return DiscardAndRevertUnpersistedBlockAsync(historyProvider);
            _atomicFlush?.DiscardCapturedBlock();
            return Task.CompletedTask;
        }

        private static bool IsEveryBlock(IFlushCadence flushCadence)
            => flushCadence is EveryBlockFlushCadence
                || (flushCadence is FixedIntervalFlushCadence k1Cadence && k1Cadence.K == 1);

        private async Task FlushWindowAtBoundaryAsync(
            IAtomicBlockFlush atomicFlush, IHistoricalStateProvider? historyProvider,
            BlockHeader header, byte[] blockHash)
        {
            var swJournal = System.Diagnostics.Stopwatch.StartNew();
            if (historyProvider != null)
            {
                await historyProvider.RecordBlockDiffAsync().ConfigureAwait(false);
            }
            swJournal.Stop();
            var flat = historyProvider != null
                ? await historyProvider.CaptureBufferAsync().ConfigureAwait(false)
                : null;
            var swFlush = System.Diagnostics.Stopwatch.StartNew();
            await atomicFlush.FlushBlockAsync(flat, (ulong)header.BlockNumber, blockHash).ConfigureAwait(false);
            swFlush.Stop();
            _logger?.LogInformation(
                "state.flush.boundary block={Block} flush_ms={FlushMs} cadence_K={K} journal_ms={JournalMs} steps={Steps}",
                header.BlockNumber, swFlush.ElapsedMilliseconds,
                _flushCadence is FixedIntervalFlushCadence fic ? (long)fic.K : 1L,
                swJournal.ElapsedMilliseconds, atomicFlush.DescribeLastFlush());
        }

        private async Task RecordOrClearBlockJournalAsync(
            IHistoricalStateProvider? historyProvider, bool boundary)
        {
            if (historyProvider != null)
            {
                if (_atomicFlush != null && !boundary)
                {
                    await historyProvider.RecordBlockDiffAsync().ConfigureAwait(false);
                }
                else
                {
                    await historyProvider.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
            }
        }

        private static void RefuseArmedBracketWithoutFlush(
            NodeCommitBlockContext? nodeCommitBlockContext, IAtomicBlockFlush? atomicFlush)
        {
            if (nodeCommitBlockContext != null && atomicFlush == null)
                throw new ArgumentException(
                    "nodeCommitBlockContext is non-null but atomicFlush is null: an armed node-history bracket " +
                    "requires atomicFlush to drain the deferred trie-node capture, or armed blocks' trie writes " +
                    "are silently lost. Pass atomicFlush (typically the same bundle cast to IAtomicBlockFlush).",
                    nameof(atomicFlush));
        }

        private static void RefuseWindowedCadenceOverNonHistoricalStore(
            IFlushCadence flushCadence, IStateStore stateStore)
        {
            if (!IsEveryBlock(flushCadence) && stateStore is not IHistoricalStateProvider)
                throw new ArgumentException(
                    $"flushCadence ({flushCadence.GetType().Name}) may produce a non-every-block boundary, but " +
                    $"stateStore ({stateStore.GetType().Name}) is not an IHistoricalStateProvider: a real window " +
                    "needs the buffered flat overlay a historical state store provides, or non-boundary blocks' " +
                    "flat writes run ahead of the durable cursor with no buffered pre-image to recover from on a " +
                    "crash. Wire an IHistoricalStateProvider state store (e.g. HistoricalStateStore), or use a " +
                    "K=1 cadence (EveryBlockFlushCadence / FixedIntervalFlushCadence(1)).",
                    nameof(flushCadence));
        }
    }
}
