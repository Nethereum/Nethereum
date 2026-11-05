using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Result = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.Result;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class Phase1History
    {
        private readonly SnapRunContext _context;

        private Phase1History(
            SnapRunContext context, Task<ParallelBlockBackfiller.BackfillResult>? backfillTask,
            CancellationTokenSource backfillCts)
        {
            _context = context;
            BackfillTask = backfillTask;
            BackfillCts = backfillCts;
        }

        internal Task<ParallelBlockBackfiller.BackfillResult>? BackfillTask { get; }

        internal CancellationTokenSource BackfillCts { get; }

        internal Task? FirstCompletion { get; private set; }

        internal Result? BackfillOnlyResult { get; private set; }

        internal static async Task<Phase1History> StartAsync(SnapRunContext context, CancellationToken ct)
        {
            var options = context.Options;
            var backfillCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var backfillTask = StartBackfillTask(context, backfillCts.Token);

            var history = new Phase1History(context, backfillTask, backfillCts);
            if (options.BackfillOnly)
            {
                await history.CompleteBackfillOnlyAsync().ConfigureAwait(false);
                return history;
            }

            if (options.Phase1First && backfillTask != null)
                await history.CompletePhase1FirstAsync(ct).ConfigureAwait(false);

            return history;
        }

        private static Task<ParallelBlockBackfiller.BackfillResult> StartBackfillTask(
            SnapRunContext context, CancellationToken bfCt)
        {
            if (!(context.Options.RunBackfill && context.Scheduler != null && context.Pool != null))
                return null;
            var logger = context.Logger;
            var backfill = new Phase1Backfill(context);
            return Task.Run(() => Phase1Backfill.RunWithStallRetryAsync(
                attemptCt => backfill.RunAsync(attemptCt),
                logger, bfCt), bfCt);
        }

        private async Task CompleteBackfillOnlyAsync()
        {
            var bfPivot = (ulong)_context.Pivots.Current.Header.BlockNumber;
            _context.Logger.LogInformation(
                "snap.bootstrap.backfill_only running Phase 1 archive to pivot {Pivot}; state sync + heal skipped", bfPivot);
            if (BackfillTask != null)
            {
                try { await BackfillTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            BackfillCts.Dispose();
            BackfillOnlyResult =
                new Result { Ran = true, PivotBlockNumber = bfPivot, SkipReason = "backfill-only (Phase 1)" };
        }

        private async Task CompletePhase1FirstAsync(CancellationToken ct)
        {
            _context.Logger.LogInformation("snap.bootstrap.phase1_first completing + compacting Phase 1 archive before Phase 2 state sync");
            var firstCompletion = HandOffToBackground(ct);
            FirstCompletion = firstCompletion;
            try { await firstCompletion.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        internal Task HandOffToBackground(CancellationToken ct)
        {
            if (BackfillTask == null)
            {
                BackfillCts.Dispose();
                return Task.CompletedTask;
            }

            _context.Logger.LogInformation(
                "snap.history.backfill continuing in background (cursor={Cursor}, target~pivot) — finalize and forward execution do NOT wait on it",
                _context.Bundle.Metadata.GetLastFetchedBody());
            return Task.Run(() => RunTailAsync(BackfillTask, ct), CancellationToken.None);
        }

        private async Task RunTailAsync(Task<ParallelBlockBackfiller.BackfillResult> backfillTask, CancellationToken ct)
        {
            try
            {
                if (!await AwaitBackfillAsync(backfillTask).ConfigureAwait(false)) return;
                await CatchUpToSettledPivotAsync().ConfigureAwait(false);
                if (BackfillCts.IsCancellationRequested) return;

                CheckpointArchive();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _context.Logger.LogWarning(ex,
                    "Phase 1 backfill failed; node has verified state at the pivot but a partial pre-pivot block archive (resumes from its cursor on restart).");
            }
        }

        private async Task<bool> AwaitBackfillAsync(Task<ParallelBlockBackfiller.BackfillResult> backfillTask)
        {
            var bf = await backfillTask.ConfigureAwait(false);
            if (BackfillCts.IsCancellationRequested) return false;
            if (bf.Ran)
            {
                _context.Logger.LogInformation(
                    "Phase 1 backfill complete: {Blocks} blocks, {Txs} txs, {Rcpts} receipts persisted up to block {End}.",
                    bf.BlocksWritten, bf.TransactionsWritten, bf.ReceiptsWritten, bf.EndBlock);
            }
            return true;
        }

        private async Task CatchUpToSettledPivotAsync()
        {
            var bundle = _context.Bundle;
            var scheduler = _context.Scheduler;
            var pool = _context.Pool;
            var logger = _context.Logger;
            if (!(_context.ExternalHeaderFollow && scheduler != null && pool != null)) return;

            var settledPivot = (ulong)_context.Pivots.Current.Header.BlockNumber;
            if (bundle.Metadata.GetLastFetchedBody() >= settledPivot) return;
            logger.LogInformation(
                "snap.phase1.settled_catchup filling bodies to the settled pivot {Pivot} (cursor={Cursor})",
                settledPivot, bundle.Metadata.GetLastFetchedBody());
            await Phase1Backfill.RunWithStallRetryAsync(
                attemptCt => Phase1Backfill.CreateBackfiller(_context)
                    .BackfillAsync(0, settledPivot, headersFromStore: true, attemptCt),
                logger, BackfillCts.Token).ConfigureAwait(false);
        }

        private void CheckpointArchive()
        {
            var bundle = _context.Bundle;
            (bundle as IBulkDurabilityBoundary)?.CheckpointBulk();
            _context.Logger.LogInformation("snap.history.backfill drained — pre-pivot archive complete");
        }

        internal async Task AbandonAsync(IBulkFlatStateSink? bulkFlat)
        {
            var logger = _context.Logger;
            if (bulkFlat != null && bulkFlat.HasBufferedRows)
                logger.LogWarning(
                    "snap.flat.sst.abandon_with_unflushed_rows this attempt's buffered flat rows are being " +
                    "discarded; the durable resume watermark (SnapTaskSet.DurableNext) was not advanced past " +
                    "the last confirmed flush, so the affected range is safely re-fetched by the next attempt.");
            bulkFlat?.Abandon();
            await StopAsync(BackfillCts, BackfillTask, logger).ConfigureAwait(false);
            BackfillCts.Dispose();
        }

        internal static async Task StopAsync(CancellationTokenSource backfillCts, Task backfill, ILogger logger)
        {
            try { backfillCts.Cancel(); }
            catch (ObjectDisposedException) { }
            if (backfill == null) return;
            if (await Task.WhenAny(backfill, Task.Delay(SnapBootstrapper.BackfillStopTimeout)).ConfigureAwait(false) != backfill)
            {
                logger.LogWarning(
                    "snap.phase1.backfill.stop_timeout timeout_sec={TimeoutSec} — the Phase-1 backfill did not observe cancellation in time; returning without it",
                    (int)SnapBootstrapper.BackfillStopTimeout.TotalSeconds);
                return;
            }
            if (backfill.IsFaulted && backfill.Exception.GetBaseException() is not OperationCanceledException)
                logger.LogWarning(backfill.Exception.GetBaseException(), "snap.phase1.backfill.stopped_faulted");
        }
    }
}
