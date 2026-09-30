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
        private static async Task<ParallelBlockBackfiller.BackfillResult> RunPhase1BackfillAsync(
            IFetchRequestScheduler? scheduler,
            IPeerPool? pool,
            IChainStoreBundle bundle,
            ILogger logger,
            IChainActivations? activations,
            bool useBackwardSkeleton,
            bool externalHeaderFollow,
            (ulong From, ulong To)? headerSweepOverride,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            RollingPivot rollingPivot,
            CancellationToken bfCt)
        {
            var worker = new PeerRequestWorker();
            var backfiller = new ParallelBlockBackfiller(
                scheduler, pool, worker, bundle,
                rootsProvider: null, logger: logger,
                activations: activations, role: "history");

            if (useBackwardSkeleton && externalHeaderFollow)
                return await RunExternalHeaderFollowBackfillAsync(
                    backfiller, bundle, scheduler, logger, headerSweepOverride, rollingPivot, bfCt).ConfigureAwait(false);

            if (useBackwardSkeleton)
                return await RunLegacySkeletonBackfillAsync(
                    backfiller, bundle, scheduler, logger, headerSweepOverride, pivotRefresher, rollingPivot, bfCt).ConfigureAwait(false);

            return await RunDefaultLoopBackfillAsync(backfiller, bundle, rollingPivot, bfCt).ConfigureAwait(false);
        }

        private static async Task<ParallelBlockBackfiller.BackfillResult> RunExternalHeaderFollowBackfillAsync(
            ParallelBlockBackfiller backfiller,
            IChainStoreBundle bundle,
            IFetchRequestScheduler? scheduler,
            ILogger logger,
            (ulong From, ulong To)? headerSweepOverride,
            RollingPivot rollingPivot,
            CancellationToken bfCt)
        {
            var target = (ulong)rollingPivot.Current.Header.BlockNumber;

            if (headerSweepOverride.HasValue)
            {
                var (repairFrom, repairTo) = headerSweepOverride.Value;
                var repairFromHash = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(repairFrom)).ConfigureAwait(false);
                if (repairFromHash == null)
                {
                    logger.LogError(
                        "snap.phase1.headers override from={From} has no stored header to anchor on; skipping repair",
                        repairFrom);
                }
                else
                {
                    logger.LogInformation(
                        "snap.phase1.headers override forced re-lay from={From} to={To} (data + cursor only; skeleton owned by the Headers job)",
                        repairFrom, repairTo);
                    bundle.Metadata.SetLastFetchedHeader(repairFrom);
                    var repairWalker = new BackwardBlockWalker(
                        scheduler, bundle,
                        new BackwardBlockWalkerOptions { HeadersOnly = true },
                        logger);
                    Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> repairLookup =
                        async (n, c) =>
                        {
                            var h = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                            return (h, h != null);
                        };
                    await repairWalker.WalkAsync(
                        repairFrom, repairFromHash, repairTo, repairLookup, bfCt,
                        noShortCircuitAboveBlock: repairTo).ConfigureAwait(false);
                }
            }

            var externalFillResult = await backfiller
                .BackfillAsync(0, target, headersFromStore: true, bfCt).ConfigureAwait(false);

            var extendedPivot = (ulong)rollingPivot.Current.Header.BlockNumber;
            if (extendedPivot > target)
            {
                externalFillResult = await backfiller
                    .BackfillAsync(0, extendedPivot, headersFromStore: true, bfCt).ConfigureAwait(false);
            }
            return externalFillResult;
        }

        private static async Task<ParallelBlockBackfiller.BackfillResult> RunLegacySkeletonBackfillAsync(
            ParallelBlockBackfiller backfiller,
            IChainStoreBundle bundle,
            IFetchRequestScheduler? scheduler,
            ILogger logger,
            (ulong From, ulong To)? headerSweepOverride,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            RollingPivot rollingPivot,
            CancellationToken bfCt)
        {
            var live = rollingPivot.Current;
            var target = (ulong)live.Header.BlockNumber;
            var anchorHash = live.Hash;

            Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> lookupLocal =
                async (n, c) =>
                {
                    var h = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                    return (h, h != null);
                };

            var fill = backfiller.BackfillAsync(0, target, headersFromStore: true, bfCt);

            var skeleton = Task.Run(async () =>
            {
                var walker = new BackwardBlockWalker(
                    scheduler, bundle,
                    new BackwardBlockWalkerOptions { HeadersOnly = true },
                    logger);

                var cursor = bundle.Metadata.GetLastFetchedHeader();
                bool cursorHeaderExists = cursor > 0 && cursor < target
                    && await bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor)).ConfigureAwait(false) != null;
                var (sweepFrom, sweepTo, sweepSource) =
                    HeaderSweepBoundsResolver.ResolveSweep(headerSweepOverride, target, cursor, cursorHeaderExists);

                byte[] sweepFromHash = sweepFrom == target
                    ? anchorHash
                    : await bundle.Blocks.GetHashByNumberAsync(new BigInteger(sweepFrom)).ConfigureAwait(false);

                if (sweepFromHash == null)
                {
                    logger.LogError(
                        "snap.phase1.headers sweep source={Source} from={From} has no stored header to anchor on; skipping header sweep (set HeadersFrom/HeadersTo to a stored block)",
                        sweepSource, sweepFrom);
                    return;
                }
                if (sweepSource == HeaderSweepSource.CursorStale)
                    logger.LogError(
                        "snap.phase1.headers cursor={Cursor} points to a MISSING header (data/cursor desync). Sweeping from pivot will NOT descend past existing headers — set HeadersFrom/HeadersTo to repair (e.g. From=<contiguous bottom> To=0).",
                        cursor);
                else
                    logger.LogInformation(
                        "snap.phase1.headers sweep source={Source} from={From} to={To} pivot={Pivot}",
                        sweepSource, sweepFrom, sweepTo, target);

                if (sweepSource == HeaderSweepSource.Override)
                {
                    bundle.Metadata.SetLastFetchedHeader(sweepFrom);
                    logger.LogInformation(
                        "snap.phase1.headers override healed the header cursor to {From} (progress + future cursor resumes now track the real frontier)",
                        sweepFrom);
                }

                bundle.Metadata.SaveHeaderSyncState(
                    HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), target));

                Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> sweepLookup =
                    headerSweepOverride.HasValue
                        ? (n, c) => Task.FromResult<(byte[], bool)>((null, false))
                        : lookupLocal;
                var sweep = await walker.WalkAsync(sweepFrom, sweepFromHash, sweepTo, sweepLookup, bfCt).ConfigureAwait(false);
                bundle.Metadata.SaveHeaderSyncState(
                    HeaderSubchains.RecordDescent(bundle.Metadata.GetHeaderSyncState(), target, sweep.SkeletonBottomBlock));

                if (pivotRefresher != null)
                {
                    var fresh = await pivotRefresher(false, bfCt).ConfigureAwait(false);
                    if (fresh.HasValue)
                    {
                        rollingPivot.Adopt(new PivotState(fresh.Value.Header, fresh.Value.Hash));
                        if ((ulong)fresh.Value.Header.BlockNumber > target)
                        {
                            var newTip = (ulong)fresh.Value.Header.BlockNumber;
                            bundle.Metadata.SaveHeaderSyncState(
                                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), newTip));
                            var catchup = await walker.WalkAsync(
                                newTip, fresh.Value.Hash, target, lookupLocal, bfCt).ConfigureAwait(false);
                            bundle.Metadata.SaveHeaderSyncState(
                                HeaderSubchains.RecordDescent(bundle.Metadata.GetHeaderSyncState(), newTip, catchup.SkeletonBottomBlock));
                        }
                    }
                }
            }, bfCt);

            await Task.WhenAll(skeleton, fill).ConfigureAwait(false);
            var fillResult = await fill.ConfigureAwait(false);
            var liveState = rollingPivot.Current;
            var livePivot = (ulong)liveState.Header.BlockNumber;
            if (livePivot > target)
            {
                var holeWalker = new BackwardBlockWalker(
                    scheduler, bundle,
                    new BackwardBlockWalkerOptions { HeadersOnly = true },
                    logger);
                var holeWalk = await holeWalker.WalkAsync(
                    livePivot, liveState.Hash, target, lookupLocal, bfCt).ConfigureAwait(false);
                logger.LogInformation(
                    "snap.phase1.pivot_extension headers [{Bottom}..{Top}] laid ({Reason}); filling bodies",
                    holeWalk.SkeletonBottomBlock, livePivot, holeWalk.ExitReason);
                bundle.Metadata.SaveHeaderSyncState(
                    HeaderSubchains.RecordDescent(
                        HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), livePivot),
                        livePivot, holeWalk.SkeletonBottomBlock));
                fillResult = await backfiller.BackfillAsync(0, livePivot, headersFromStore: true, bfCt).ConfigureAwait(false);
            }
            return fillResult;
        }

        private static async Task<ParallelBlockBackfiller.BackfillResult> RunDefaultLoopBackfillAsync(
            ParallelBlockBackfiller backfiller,
            IChainStoreBundle bundle,
            RollingPivot rollingPivot,
            CancellationToken bfCt)
        {
            ParallelBlockBackfiller.BackfillResult last = null!;
            while (!bfCt.IsCancellationRequested)
            {
                var target = (ulong)rollingPivot.Current.Header.BlockNumber;
                var resume = bundle.Metadata.GetLastFetchedHeader();
                if (resume >= target) break;
                last = await backfiller.BackfillAsync(0, target, bfCt).ConfigureAwait(false);
            }
            return last ?? new ParallelBlockBackfiller.BackfillResult { Ran = false };
        }
    }
}
