using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static class SnapSyncOrchestrator
    {
        public const int PivotFetchInitialBackoffMs = 500;

        public const int PivotFetchMaxBackoffMs = 10_000;

        public const int PivotFetchMaxConsecutiveFailures = 6;

        public const int PivotFetchEscalationDelayMs = 30_000;

        public const int SnapAttemptInitialBackoffMs = 2_000;

        public const int SnapAttemptMaxBackoffMs = 30_000;

        public const int SnapAttemptEscalationThreshold = 3;

        public const int SnapAttemptMaxBackoffMsEscalated = 600_000;

        public const ulong PivotTrailDistance = 64;

        public const ulong PivotServeTrailDistance = 32;

        public const ulong PivotStaleDistanceBlocks = PivotTiming.StaleDistanceBlocks;

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SnapSyncOrchestrator.RunAsync — top-level snap driver")]
        public static async Task<SnapBootstrapper.Result> RunAsync(
            IChainStoreBundle bundle,
            IPeerPool? pool,
            IFetchRequestScheduler? scheduler,
            ICanonicalStateRootSource canonicalTip,
            IChainActivations activations,
            ILogger logger,
            SnapSyncOrchestratorOptions options = null,
            CancellationToken ct = default)
        {
            options ??= new SnapSyncOrchestratorOptions();
            var skipped = ResumeRouter.SkipIfStateCommitted(bundle, logger);
            if (skipped != null) return skipped;

            if (pool == null || scheduler == null) return SkipWithoutPeers(logger);

            LogEntry(pool, canonicalTip, logger);

            var selfFollow = SelfHeaderFollow.Start(scheduler, bundle, canonicalTip, options, logger, ct);

            try
            {
                ulong savedPivotFloor = ReadSavedPivotFloor(bundle, logger);
                return await RunAttemptsAsync(
                    bundle, pool, scheduler, canonicalTip, activations, logger, options, selfFollow, savedPivotFloor, ct).ConfigureAwait(false);
            }
            finally
            {
                await selfFollow.DrainAsync();
            }
        }

        private static SnapBootstrapper.Result SkipWithoutPeers(ILogger logger)
        {
            logger.LogInformation("snap.bootstrap.skip reason=no_pool_or_scheduler");
            return new SnapBootstrapper.Result { Ran = false, SkipReason = "no peer pool / scheduler registered" };
        }

        private static void LogEntry(IPeerPool pool, ICanonicalStateRootSource canonicalTip, ILogger logger)
        {
            var trustedSource = canonicalTip?.Name ?? "<peer-pool-sampling>";
            logger.LogInformation("snap.bootstrap.entry peer_count={PeerCount} trusted_source={TrustedSource}",
                pool.ActivePeers.Count, trustedSource);
        }

        private static async Task<SnapBootstrapper.Result> RunAttemptsAsync(
            IChainStoreBundle bundle, IPeerPool pool, IFetchRequestScheduler scheduler,
            ICanonicalStateRootSource canonicalTip, IChainActivations activations, ILogger logger,
            SnapSyncOrchestratorOptions options, SelfHeaderFollow selfFollow, ulong savedPivotFloor, CancellationToken ct)
        {
            var retry = new SnapAttemptRetry(logger);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                retry.NextAttempt();

                var acquisition = new PivotAcquisition(
                    pool, scheduler, logger, canonicalTip,
                    selfFollow.HeaderFollowActive ? bundle : null, options.PivotStaleDistanceBlocks);
                var pivot = await TryFetchPivotAsync(acquisition, savedPivotFloor, ct).ConfigureAwait(false);
                if (pivot == null)
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "cancelled while fetching pivot" };

                try
                {
                    return await RunSnapAttemptAsync(
                        bundle, pool, scheduler, canonicalTip, activations, logger, options, selfFollow, pivot.Value, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "cancelled during snap-bootstrap" };
                }
                catch (SnapSyncClient.SnapTaskSetStalledException ex) when (!ct.IsCancellationRequested)
                {
                    await retry.BackOffAsync(RetryKind.TasksetStalled, ex, ct).ConfigureAwait(false);
                }
                catch (SnapSyncClient.SnapTaskLeaseStalledException ex) when (!ct.IsCancellationRequested)
                {
                    await retry.BackOffAsync(RetryKind.LeaseStalled, ex, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    await retry.BackOffAsync(RetryKind.Other, ex, ct).ConfigureAwait(false);
                }
            }
        }

        private static async Task<(BlockHeader Header, byte[] Hash)?> TryFetchPivotAsync(
            PivotAcquisition acquisition, ulong savedPivotFloor, CancellationToken ct)
        {
            try
            {
                return await acquisition.FetchAsync(savedPivotFloor, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private static async Task<SnapBootstrapper.Result> RunSnapAttemptAsync(
            IChainStoreBundle bundle, IPeerPool pool, IFetchRequestScheduler scheduler,
            ICanonicalStateRootSource canonicalTip, IChainActivations activations, ILogger logger,
            SnapSyncOrchestratorOptions options, SelfHeaderFollow selfFollow,
            (BlockHeader Header, byte[] Hash) pivot, CancellationToken ct)
        {
            var (pivotHeader, pivotHash) = pivot;
            logger.LogInformation(
                "Snap-bootstrap: pivot header received block={Block} hash=0x{Hash} stateRoot=0x{Root}; running snap stream through scheduler...",
                pivotHeader.BlockNumber, pivotHash.ToHex(), pivotHeader.StateRoot.ToHex());

            var snapPeer = new SchedulerSnapPeer(scheduler);
            var rollingPivot = new SnapBootstrapper.RollingPivot(pivotHeader, pivotHash);
            var refresher = PivotRefresh.Build(
                pool, scheduler, canonicalTip, logger, rollingPivot,
                bundle: selfFollow.HeaderFollowActive ? bundle : null,
                staleDistanceBlocks: options.PivotStaleDistanceBlocks);

            var result = await SnapBootstrapper.RunAsync(
                bundle, snapPeer, pivotHeader, pivotHash, logger,
                BuildRunOptions(pool, scheduler, activations, options, refresher, rollingPivot, selfFollow.HeaderFollowActive),
                ct).ConfigureAwait(false);

            logger.LogInformation(
                "Snap-bootstrap: completed at pivot block {Block} ({Accounts} accounts, {Slots} slots, {Codes} bytecodes).",
                result.PivotBlockNumber, result.AccountCount, result.SlotCount, result.BytecodeCount);

            selfFollow.HandOffTo(result.HistoryBackfill);
            await DrainStateCompactionInlineAsync(result, options, logger);
            return result;
        }

        private static SnapRunOptions BuildRunOptions(
            IPeerPool pool, IFetchRequestScheduler scheduler, IChainActivations activations,
            SnapSyncOrchestratorOptions options,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher,
            SnapBootstrapper.RollingPivot rollingPivot, bool headerFollowActive)
            => new SnapRunOptions
            {
                Scheduler = scheduler,
                PivotRefresher = refresher,
                RunBackfill = options.RunHistoryBackfill,
                Activations = activations,
                Pool = pool,
                Metrics = options.Metrics,
                UseBackwardSkeleton = options.UseBackwardSkeleton,
                RootRefreshIntervalMs = options.RootRefreshIntervalMs,
                HeaderSweepOverride = options.HeaderSweepOverride,
                BackfillOnly = options.BackfillOnly,
                Phase1First = options.Phase1First,
                AccountConcurrency = options.AccountConcurrency,
                LargeContractConcurrency = options.LargeContractConcurrency,
                ExternalHeaderFollow = headerFollowActive,
                BalHealEnabled = options.BalHealEnabled,
                RollingPivot = rollingPivot,
            };

        private static ulong ReadSavedPivotFloor(IChainStoreBundle bundle, ILogger logger)
        {
            ulong savedPivotFloor = bundle.Metadata.GetSnapSyncState()?.PivotBlockNumber ?? 0;
            if (savedPivotFloor > 0)
                logger.LogInformation("snap.bootstrap.resume_floor saved_pivot={Floor} — will not adopt a pivot below this", savedPivotFloor);
            return savedPivotFloor;
        }

        private static async Task DrainStateCompactionInlineAsync(
            SnapBootstrapper.Result result, SnapSyncOrchestratorOptions options, ILogger logger)
        {
            if (options.HeaderFollow == null && !result.StateCompaction.IsCompleted)
            {
                logger.LogInformation("snap.state.compact draining inline (no long-lived host to own it)");
                await result.StateCompaction.ConfigureAwait(false);
            }
        }

        public static bool PivotIsStale(ulong currentPivotBlock, ulong tipBlock)
            => PivotIsStale(currentPivotBlock, tipBlock, PivotStaleDistanceBlocks);

        public static bool PivotIsStale(ulong currentPivotBlock, ulong tipBlock, ulong staleDistanceBlocks)
            => tipBlock > currentPivotBlock + staleDistanceBlocks;

        public static bool ShouldRefreshTrailedPivot(
            bool forceFresh, ulong currentPivotBlock, ulong tipBlock,
            ulong candidatePivotBlock, ulong lastReturnedPivotBlock)
            => ShouldRefreshTrailedPivot(
                forceFresh, currentPivotBlock, tipBlock, candidatePivotBlock,
                lastReturnedPivotBlock, PivotStaleDistanceBlocks);

        public static bool ShouldRefreshTrailedPivot(
            bool forceFresh, ulong currentPivotBlock, ulong tipBlock,
            ulong candidatePivotBlock, ulong lastReturnedPivotBlock, ulong staleDistanceBlocks)
        {
            if (!forceFresh && !PivotIsStale(currentPivotBlock, tipBlock, staleDistanceBlocks)) return false;
            return candidatePivotBlock > lastReturnedPivotBlock;
        }

        public static Task<(BlockHeader Header, byte[] Hash)?> TrailedPivotFromStoreAsync(
            IChainStoreBundle bundle, CanonicalTip tip, CancellationToken ct)
            => TrailedPivot.FromStoreAsync(bundle, tip, ct);

        public static bool CanonicalPivotMeetsResumeFloor(ulong tipBlock, ulong savedPivotFloor)
            => savedPivotFloor == 0 || tipBlock >= savedPivotFloor;

        public static (bool Escalated, int BackoffCapMs) ComputeRetryEscalation(int attempt)
        {
            bool escalated = attempt >= SnapAttemptEscalationThreshold;
            int cap = escalated ? SnapAttemptMaxBackoffMsEscalated : SnapAttemptMaxBackoffMs;
            return (escalated, cap);
        }

        public static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> BuildPivotRefresher(
            IPeerPool pool, IFetchRequestScheduler scheduler, ICanonicalStateRootSource canonicalTip, ILogger logger,
            SnapBootstrapper.RollingPivot rollingPivot, IChainStoreBundle bundle = null,
            ulong staleDistanceBlocks = PivotStaleDistanceBlocks)
            => PivotRefresh.Build(pool, scheduler, canonicalTip, logger, rollingPivot, bundle, staleDistanceBlocks);
    }
}

