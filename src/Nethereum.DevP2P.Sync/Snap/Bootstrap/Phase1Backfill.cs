using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class Phase1Backfill
    {
        private readonly SnapRunContext _context;

        private sealed record SweepPlan(ulong From, ulong To, HeaderSweepSource Source, ulong Cursor, byte[] FromHash);

        internal Phase1Backfill(SnapRunContext context) => _context = context;

        private const int Phase1StallRetryInitialBackoffMs = 2_000;
        private const int Phase1StallRetryMaxBackoffMs = 30_000;

        internal static async Task<ParallelBlockBackfiller.BackfillResult> RunWithStallRetryAsync(
            Func<CancellationToken, Task<ParallelBlockBackfiller.BackfillResult>> attempt,
            ILogger logger,
            CancellationToken ct,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            delay ??= Task.Delay;
            var backoff = new Backoff(Phase1StallRetryInitialBackoffMs);

            while (true)
            {
                try
                {
                    return await attempt(ct).ConfigureAwait(false);
                }
                catch (BackfillStalledException ex)
                {
                    var wait = TimeSpan.FromMilliseconds(backoff.Current);
                    logger.LogWarning(
                        "snap.phase1.stalled {Message} — retrying in {BackoffSeconds:F0}s (rebuilding the backfill queue)",
                        ex.Message, wait.TotalSeconds);
                    await delay(wait, ct).ConfigureAwait(false);
                    backoff.Advance(Phase1StallRetryMaxBackoffMs);
                }
            }
        }

        internal static ParallelBlockBackfiller CreateBackfiller(SnapRunContext context)
            => new ParallelBlockBackfiller(
                context.Scheduler, context.Pool, new PeerRequestWorker(), context.Bundle,
                rootsProvider: null, logger: context.Logger,
                activations: context.Activations, role: "history");

        internal async Task<ParallelBlockBackfiller.BackfillResult> RunAsync(CancellationToken bfCt)
        {
            var backfiller = CreateBackfiller(_context);

            var useBackwardSkeleton = _context.Options.UseBackwardSkeleton;
            if (useBackwardSkeleton && _context.ExternalHeaderFollow)
                return await RunExternalHeaderFollowAsync(backfiller, bfCt).ConfigureAwait(false);

            if (useBackwardSkeleton)
                return await RunLegacySkeletonAsync(backfiller, bfCt).ConfigureAwait(false);

            return await RunDefaultLoopAsync(backfiller, bfCt).ConfigureAwait(false);
        }

        private async Task<ParallelBlockBackfiller.BackfillResult> RunExternalHeaderFollowAsync(
            ParallelBlockBackfiller backfiller, CancellationToken bfCt)
        {
            var target = (ulong)_context.Pivots.Current.Header.BlockNumber;

            if (_context.Options.HeaderSweepOverride.HasValue)
                await RepairHeadersAsync(_context.Options.HeaderSweepOverride.Value, bfCt).ConfigureAwait(false);

            return await FillToPivotAsync(backfiller, target, bfCt).ConfigureAwait(false);
        }

        private async Task RepairHeadersAsync((ulong From, ulong To) range, CancellationToken bfCt)
        {
            var (repairFrom, repairTo) = range;
            var repairFromHash = await _context.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(repairFrom)).ConfigureAwait(false);
            if (repairFromHash == null)
            {
                _context.Logger.LogError(
                    "snap.phase1.headers override from={From} has no stored header to anchor on; skipping repair",
                    repairFrom);
                return;
            }

            _context.Logger.LogInformation(
                "snap.phase1.headers override forced re-lay from={From} to={To} (data + cursor only; skeleton owned by the Headers job)",
                repairFrom, repairTo);
            _context.Bundle.Metadata.SetLastFetchedHeader(repairFrom);
            var repairWalker = HeaderSkeleton.CreateWalker(_context.Scheduler, _context.Bundle, _context.Logger);
            Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> repairLookup = HeaderSkeleton.StoredHashLookup(_context.Bundle);
            await repairWalker.WalkAsync(
                repairFrom, repairFromHash, repairTo, repairLookup, bfCt,
                noShortCircuitAboveBlock: repairTo).ConfigureAwait(false);
        }

        private async Task<ParallelBlockBackfiller.BackfillResult> FillToPivotAsync(
            ParallelBlockBackfiller backfiller, ulong target, CancellationToken bfCt)
        {
            var externalFillResult = await backfiller
                .BackfillAsync(0, target, headersFromStore: true, bfCt).ConfigureAwait(false);

            var extendedPivot = (ulong)_context.Pivots.Current.Header.BlockNumber;
            if (extendedPivot > target)
            {
                externalFillResult = await backfiller
                    .BackfillAsync(0, extendedPivot, headersFromStore: true, bfCt).ConfigureAwait(false);
            }
            return externalFillResult;
        }

        private async Task<ParallelBlockBackfiller.BackfillResult> RunLegacySkeletonAsync(
            ParallelBlockBackfiller backfiller, CancellationToken bfCt)
        {
            var live = _context.Pivots.Current;
            var target = (ulong)live.Header.BlockNumber;
            var anchorHash = live.Hash;

            var fill = backfiller.BackfillAsync(0, target, headersFromStore: true, bfCt);

            var skeleton = Task.Run(() => LaySkeletonAsync(target, anchorHash, bfCt), bfCt);

            await Task.WhenAll(skeleton, fill).ConfigureAwait(false);
            var fillResult = await fill.ConfigureAwait(false);
            return await ExtendIfPivotRolledAsync(backfiller, target, fillResult, bfCt).ConfigureAwait(false);
        }

        private async Task LaySkeletonAsync(ulong target, byte[] anchorHash, CancellationToken bfCt)
        {
            var walker = HeaderSkeleton.CreateWalker(_context.Scheduler, _context.Bundle, _context.Logger);

            var plan = await PlanSweepAsync(target, anchorHash).ConfigureAwait(false);
            if (plan.FromHash == null)
            {
                _context.Logger.LogError(
                    "snap.phase1.headers sweep source={Source} from={From} has no stored header to anchor on; skipping header sweep (set HeadersFrom/HeadersTo to a stored block)",
                    plan.Source, plan.From);
                return;
            }
            LogSweep(plan, target);
            HealCursorIfOverridden(plan);

            await WalkSweepAsync(walker, plan, target, bfCt).ConfigureAwait(false);

            await CatchUpToFreshPivotAsync(walker, target, bfCt).ConfigureAwait(false);
        }

        private async Task<SweepPlan> PlanSweepAsync(ulong target, byte[] anchorHash)
        {
            var cursor = _context.Bundle.Metadata.GetLastFetchedHeader();
            bool cursorHeaderExists = cursor > 0 && cursor < target
                && await _context.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor)).ConfigureAwait(false) != null;
            var (sweepFrom, sweepTo, sweepSource) =
                HeaderSweepBoundsResolver.ResolveSweep(_context.Options.HeaderSweepOverride, target, cursor, cursorHeaderExists);

            byte[] sweepFromHash = sweepFrom == target
                ? anchorHash
                : await _context.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(sweepFrom)).ConfigureAwait(false);

            return new SweepPlan(sweepFrom, sweepTo, sweepSource, cursor, sweepFromHash);
        }

        private void LogSweep(SweepPlan plan, ulong target)
        {
            if (plan.Source == HeaderSweepSource.CursorStale)
                _context.Logger.LogError(
                    "snap.phase1.headers cursor={Cursor} points to a MISSING header (data/cursor desync). Sweeping from pivot will NOT descend past existing headers — set HeadersFrom/HeadersTo to repair (e.g. From=<contiguous bottom> To=0).",
                    plan.Cursor);
            else
                _context.Logger.LogInformation(
                    "snap.phase1.headers sweep source={Source} from={From} to={To} pivot={Pivot}",
                    plan.Source, plan.From, plan.To, target);
        }

        private void HealCursorIfOverridden(SweepPlan plan)
        {
            if (plan.Source != HeaderSweepSource.Override) return;
            _context.Bundle.Metadata.SetLastFetchedHeader(plan.From);
            _context.Logger.LogInformation(
                "snap.phase1.headers override healed the header cursor to {From} (progress + future cursor resumes now track the real frontier)",
                plan.From);
        }

        private async Task WalkSweepAsync(
            BackwardBlockWalker walker, SweepPlan plan, ulong target, CancellationToken bfCt)
        {
            Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> sweepLookup =
                _context.Options.HeaderSweepOverride.HasValue
                    ? (n, c) => Task.FromResult<(byte[], bool)>((null, false))
                    : HeaderSkeleton.StoredHashLookup(_context.Bundle);
            await HeaderSkeleton.WalkAndRecordAsync(
                walker, _context.Bundle, target, plan.From, plan.FromHash, plan.To, sweepLookup, bfCt).ConfigureAwait(false);
        }

        private async Task CatchUpToFreshPivotAsync(
            BackwardBlockWalker walker, ulong target, CancellationToken bfCt)
        {
            if (_context.PivotRefresher == null) return;
            var fresh = await _context.Pivots.FetchFreshAsync(false, bfCt).ConfigureAwait(false);
            if (!fresh.HasValue) return;
            if ((ulong)fresh.Value.Header.BlockNumber <= target) return;
            var newTip = (ulong)fresh.Value.Header.BlockNumber;
            await HeaderSkeleton.WalkAndRecordAsync(
                walker, _context.Bundle, newTip, newTip, fresh.Value.Hash, target,
                HeaderSkeleton.StoredHashLookup(_context.Bundle), bfCt).ConfigureAwait(false);
        }

        private async Task<ParallelBlockBackfiller.BackfillResult> ExtendIfPivotRolledAsync(
            ParallelBlockBackfiller backfiller, ulong target,
            ParallelBlockBackfiller.BackfillResult fillResult, CancellationToken bfCt)
        {
            var liveState = _context.Pivots.Current;
            var livePivot = (ulong)liveState.Header.BlockNumber;
            if (livePivot <= target) return fillResult;

            var holeWalker = HeaderSkeleton.CreateWalker(_context.Scheduler, _context.Bundle, _context.Logger);
            var holeWalk = await holeWalker.WalkAsync(
                livePivot, liveState.Hash, target, HeaderSkeleton.StoredHashLookup(_context.Bundle), bfCt).ConfigureAwait(false);
            _context.Logger.LogInformation(
                "snap.phase1.pivot_extension headers [{Bottom}..{Top}] laid ({Reason}); filling bodies",
                holeWalk.SkeletonBottomBlock, livePivot, holeWalk.ExitReason);
            HeaderSkeleton.OpenTipAndRecordDescent(_context.Bundle, livePivot, holeWalk.SkeletonBottomBlock);
            return await backfiller.BackfillAsync(0, livePivot, headersFromStore: true, bfCt).ConfigureAwait(false);
        }

        private async Task<ParallelBlockBackfiller.BackfillResult> RunDefaultLoopAsync(
            ParallelBlockBackfiller backfiller, CancellationToken bfCt)
        {
            ParallelBlockBackfiller.BackfillResult last = null!;
            while (!bfCt.IsCancellationRequested)
            {
                var target = (ulong)_context.Pivots.Current.Header.BlockNumber;
                var resume = _context.Bundle.Metadata.GetLastFetchedHeader();
                if (resume >= target) break;
                last = await backfiller.BackfillAsync(0, target, bfCt).ConfigureAwait(false);
            }
            return last ?? new ParallelBlockBackfiller.BackfillResult { Ran = false };
        }
    }
}
