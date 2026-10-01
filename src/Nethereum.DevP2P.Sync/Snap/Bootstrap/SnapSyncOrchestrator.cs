using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

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

        public const ulong PivotStaleDistanceBlocks = 120;

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

        public static async Task<(BlockHeader Header, byte[] Hash)?> TrailedPivotFromStoreAsync(
            IChainStoreBundle bundle, CanonicalTip tip, CancellationToken ct)
        {
            if (tip == null || tip.BlockHash == null || tip.BlockHash.Length != 32) return null;
            if (tip.BlockNumber < 1) return null;

            ulong laidTip = HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState());
            ulong effectiveTip = Math.Min(tip.BlockNumber, laidTip);
            if (effectiveTip == 0 || effectiveTip < PivotServeTrailDistance + 1) return null;

            ulong pivotNumber = effectiveTip - PivotServeTrailDistance;

            byte[] expectedHash = null;
            for (ulong n = effectiveTip; ; n--)
            {
                ct.ThrowIfCancellationRequested();
                var storedHash = await bundle.Blocks.GetHashByNumberAsync(new System.Numerics.BigInteger(n)).ConfigureAwait(false);
                if (storedHash == null) return null;
                if (expectedHash != null && !ByteUtil.AreEqual(storedHash, expectedHash)) return null;
                var header = await bundle.Blocks.GetByNumberAsync(new System.Numerics.BigInteger(n)).ConfigureAwait(false);
                if (header == null) return null;
                if (n == pivotNumber)
                {
                    if (header.StateRoot == null || header.StateRoot.Length != 32) return null;
                    return (header, storedHash);
                }
                if (header.ParentHash == null || header.ParentHash.Length != 32) return null;
                expectedHash = header.ParentHash;
            }
        }

        public static bool CanonicalPivotMeetsResumeFloor(ulong tipBlock, ulong savedPivotFloor)
            => savedPivotFloor == 0 || tipBlock >= savedPivotFloor;

        public static (bool Escalated, int BackoffCapMs) ComputeRetryEscalation(int attempt)
        {
            bool escalated = attempt >= SnapAttemptEscalationThreshold;
            int cap = escalated ? SnapAttemptMaxBackoffMsEscalated : SnapAttemptMaxBackoffMs;
            return (escalated, cap);
        }

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
            var lastBlock = bundle.Metadata.GetLastBlock();
            if (lastBlock > 0)
            {
                logger.LogInformation("snap.bootstrap.skip reason=committed_state block={Block}", lastBlock);
                return new SnapBootstrapper.Result { Ran = false, SkipReason = $"existing state at block {lastBlock}" };
            }

            if (pool == null || scheduler == null)
            {
                logger.LogInformation("snap.bootstrap.skip reason=no_pool_or_scheduler");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "no peer pool / scheduler registered" };
            }

            var trustedSource = canonicalTip?.Name ?? "<peer-pool-sampling>";
            logger.LogInformation("snap.bootstrap.entry peer_count={PeerCount} trusted_source={TrustedSource}",
                pool.ActivePeers.Count, trustedSource);

            var (selfFollowCts, selfFollowTask, headerFollowActive) =
                StartSelfFollowHeaders(scheduler, bundle, canonicalTip, options, logger, ct);

            try
            {

            ulong savedPivotFloor = ReadSavedPivotFloor(bundle, logger);

            int attempt = 0;
            int retryBackoffMs = SnapAttemptInitialBackoffMs;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                attempt++;

                BlockHeader pivotHeader;
                byte[] pivotHash;
                try
                {
                    (pivotHeader, pivotHash) = await FetchPivotWithRetryAsync(
                        pool, scheduler, logger, ct, canonicalTip, savedPivotFloor,
                        bundle: headerFollowActive ? bundle : null,
                        floorOnReturnedPivot: options.BalHealEnabled).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "cancelled while fetching pivot" };
                }

                try
                {
                    logger.LogInformation(
                        "Snap-bootstrap: pivot header received block={Block} hash=0x{Hash} stateRoot=0x{Root}; running snap stream through scheduler...",
                        pivotHeader.BlockNumber, pivotHash.ToHex(), pivotHeader.StateRoot.ToHex());

                    var snapPeer = new SchedulerSnapPeer(scheduler);
                    var rollingPivot = new SnapBootstrapper.RollingPivot(pivotHeader, pivotHash);
                    var refresher = BuildPivotRefresher(
                        pool, scheduler, canonicalTip, logger, rollingPivot,
                        bundle: headerFollowActive ? bundle : null,
                        staleDistanceBlocks: options.PivotStaleDistanceBlocks);

                    var result = await SnapBootstrapper.RunAsync(
                        bundle, snapPeer, pivotHeader, pivotHash, logger,
                        new SnapRunOptions
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
                            EnableFlatReconcile = options.EnableFlatReconcile,
                            FinalizeVerify = options.FinalizeVerify,
                            ExternalHeaderFollow = headerFollowActive,
                            BalHealEnabled = options.BalHealEnabled,
                            RollingPivot = rollingPivot,
                        },
                        ct).ConfigureAwait(false);

                    logger.LogInformation(
                        "Snap-bootstrap: completed at pivot block {Block} ({Accounts} accounts, {Slots} slots, {Codes} bytecodes).",
                        result.PivotBlockNumber, result.AccountCount, result.SlotCount, result.BytecodeCount);

                    if (selfFollowCts != null && !result.HistoryBackfill.IsCompleted)
                    {
                        var jobCts = selfFollowCts;
                        var jobTask = selfFollowTask;
                        selfFollowCts = null;
                        _ = result.HistoryBackfill.ContinueWith(_ => DrainSelfFollowAsync(jobCts, jobTask, logger),
                            CancellationToken.None).Unwrap();
                    }
                    await DrainStateCompactionInlineAsync(result, options, logger);
                    return result;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "cancelled during snap-bootstrap" };
                }
                catch (SnapSyncClient.SnapTaskSetStalledException ex) when (!ct.IsCancellationRequested)
                {
                    (bool escalated, int cap) = ComputeRetryEscalation(attempt);
                    if (escalated)
                        logger.LogError(ex,
                            "snap.phase2.recycle reason=taskset_stalled attempt={Attempt}; {Threshold}+ consecutive failures - retrying in {BackoffMs}ms.",
                            attempt, SnapAttemptEscalationThreshold, retryBackoffMs);
                    else
                        logger.LogWarning(ex,
                            "snap.phase2.recycle reason=taskset_stalled attempt={Attempt}; retrying in {BackoffMs}ms",
                            attempt, retryBackoffMs);
                    await Task.Delay(retryBackoffMs, ct).ConfigureAwait(false);
                    retryBackoffMs = Math.Min(retryBackoffMs * 2, cap);
                }
                catch (SnapSyncClient.SnapTaskLeaseStalledException ex) when (!ct.IsCancellationRequested)
                {
                    (bool escalated, int cap) = ComputeRetryEscalation(attempt);
                    if (escalated)
                        logger.LogError(ex,
                            "snap.phase2.recycle reason=lease_stalled attempt={Attempt}; {Threshold}+ consecutive failures - retrying in {BackoffMs}ms.",
                            attempt, SnapAttemptEscalationThreshold, retryBackoffMs);
                    else
                        logger.LogWarning(ex,
                            "snap.phase2.recycle reason=lease_stalled attempt={Attempt}; retrying in {BackoffMs}ms",
                            attempt, retryBackoffMs);
                    await Task.Delay(retryBackoffMs, ct).ConfigureAwait(false);
                    retryBackoffMs = Math.Min(retryBackoffMs * 2, cap);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    (bool escalated, int cap) = ComputeRetryEscalation(attempt);
                    if (escalated)
                        logger.LogError(ex,
                            "snap.bootstrap.retry.stalled attempt={Attempt} reason={Reason}; {Threshold}+ consecutive " +
                            "failures — retrying in {BackoffMs}ms. This node has not reached Phase 4 after multiple " +
                            "full bootstrap attempts; investigate (no genesis fallback).",
                            attempt, ex.GetType().Name, SnapAttemptEscalationThreshold, retryBackoffMs);
                    else
                        logger.LogWarning(ex,
                            "snap.bootstrap.retry attempt={Attempt} reason={Reason}; retrying in {BackoffMs}ms (no genesis fallback)",
                            attempt, ex.GetType().Name, retryBackoffMs);
                    await Task.Delay(retryBackoffMs, ct).ConfigureAwait(false);
                    retryBackoffMs = Math.Min(retryBackoffMs * 2, cap);
                }
            }
            }
            finally
            {
                if (selfFollowCts != null)
                    await DrainSelfFollowAsync(selfFollowCts, selfFollowTask, logger);
            }
        }

        private static (CancellationTokenSource? Cts, Task? FollowTask, bool HeaderFollowActive) StartSelfFollowHeaders(
            IFetchRequestScheduler scheduler, IChainStoreBundle bundle, ICanonicalStateRootSource canonicalTip,
            SnapSyncOrchestratorOptions options, ILogger logger, CancellationToken ct)
        {
            if (options.HeaderFollow != null)
                return (null, null, true);
            if (canonicalTip == null || !options.UseBackwardSkeleton)
                return (null, null, false);

            var headersOnlyWalker = new BackwardBlockWalker(
                scheduler, bundle, new BackwardBlockWalkerOptions { HeadersOnly = true }, logger);
            BackwardWalkerDelegate headersOnly = async (fromBlock, fromHash, toBlock, b, c, noShortCircuitAboveBlock) =>
            {
                Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> lookup = async (bn, cc) =>
                {
                    cc.ThrowIfCancellationRequested();
                    var h = await b.Blocks.GetHashByNumberAsync(bn).ConfigureAwait(false);
                    return (h, h != null);
                };
                var r = await headersOnlyWalker.WalkAsync(fromBlock, fromHash, toBlock, lookup, c, noShortCircuitAboveBlock).ConfigureAwait(false);
                return new WalkerOutcome(r.ExitReason, r.HeadersWritten, r.DivergenceBlock,
                    SkeletonBottomBlock: r.SkeletonBottomBlock, MetExistingStore: r.MetExistingStore);
            };
            var resolver = new AncestorResolver(scheduler, bundle);
            var selfFollowOptions = new HeaderFollowOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(options.RootRefreshIntervalMs),
                DescentIdleInterval = TimeSpan.FromMilliseconds(options.RootRefreshIntervalMs),
            };
            var selfFollow = new HeaderFollowService(
                canonicalTip, headersOnly, selfFollowOptions, logger,
                ancestorResolver: resolver.FindAsync);
            var selfFollowCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var selfFollowTask = Task.Run(() => selfFollow.RunAsync(bundle, selfFollowCts.Token), selfFollowCts.Token);
            return (selfFollowCts, selfFollowTask, true);
        }

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

        private static async Task DrainSelfFollowAsync(CancellationTokenSource cts, Task task, ILogger logger)
        {
            cts.Cancel();
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { logger.LogDebug(ex, "snap.headers.self_follow_drain"); }
            cts.Dispose();
        }

        public static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> BuildPivotRefresher(
            IPeerPool pool, IFetchRequestScheduler scheduler, ICanonicalStateRootSource canonicalTip, ILogger logger,
            SnapBootstrapper.RollingPivot rollingPivot, IChainStoreBundle bundle = null,
            ulong staleDistanceBlocks = PivotStaleDistanceBlocks)
        {
            return async (forceFresh, refreshCt) =>
            {
                try
                {
                    ulong currentTipBlock;
                    if (canonicalTip != null && bundle != null)
                    {
                        var tip = await canonicalTip.GetLatestAsync(refreshCt).ConfigureAwait(false);
                        if (tip == null || tip.BlockNumber <= PivotTrailDistance)
                        {
                            logger?.LogDebug("snap.pivot.refresh_noop reason=no_canonical_tip_or_too_low");
                            return null;
                        }
                        var current = (ulong)rollingPivot.Current.Header.BlockNumber;
                        if (!forceFresh && !PivotIsStale(current, tip.BlockNumber, staleDistanceBlocks))
                        {
                            logger?.LogDebug("snap.pivot.refresh_noop current={Current} tip={Tip} stale=false", current, tip.BlockNumber);
                            return null;
                        }
                        var trailed = await TrailedPivotFromStoreAsync(bundle, tip, refreshCt).ConfigureAwait(false);
                        if (!trailed.HasValue)
                        {
                            logger?.LogWarning("snap.pivot.refresh_waiting_for_trail current={Current} tip={Tip} reason=missing_trailed_header", current, tip.BlockNumber);
                            return null;
                        }
                        var trailedPivotBlock = (ulong)trailed.Value.Header.BlockNumber;
                        if (!ShouldRefreshTrailedPivot(forceFresh, current, tip.BlockNumber,
                                trailedPivotBlock, (ulong)rollingPivot.Current.Header.BlockNumber,
                                staleDistanceBlocks))
                            return null;
                        return trailed;
                    }
                    if (canonicalTip != null)
                    {
                        var tip = await canonicalTip.GetLatestAsync(refreshCt).ConfigureAwait(false);
                        if (tip == null || tip.BlockNumber <= PivotTrailDistance)
                        {
                            logger?.LogDebug("snap.pivot.refresh_noop reason=no_canonical_tip_or_too_low");
                            return null;
                        }
                        if (tip.StateRoot != null && tip.StateRoot.Length == 32
                            && tip.BlockHash != null && tip.BlockHash.Length == 32)
                        {
                            return (new BlockHeader { BlockNumber = tip.BlockNumber, StateRoot = tip.StateRoot }, tip.BlockHash);
                        }
                        currentTipBlock = tip.BlockNumber;
                    }
                    else
                    {
                        var currentPeerLatest = pool.ActivePeers
                            .OfType<SyncPeerSession>()
                            .Where(s => s.PeerLatestBlock > PivotTrailDistance)
                            .Select(s => s.PeerLatestBlock)
                            .DefaultIfEmpty(0UL)
                            .Max();
                        if (currentPeerLatest == 0) return null;
                        currentTipBlock = currentPeerLatest;
                    }
                    var newPivotBlock = currentTipBlock - PivotTrailDistance;
                    var newHeaders = await scheduler.FetchHeadersAsync(newPivotBlock, limit: 1, refreshCt).ConfigureAwait(false);
                    if (newHeaders == null || newHeaders.Count == 0) return null;
                    var newHeader = newHeaders[0];
                    var newHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(newHeader);
                    return (newHeader, newHash);
                }
                catch (OperationCanceledException) when (refreshCt.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "snap.pivot.refresh_failed error_type={ErrorType}", ex.GetType().Name);
                    return null;
                }
            };
        }

        private static async Task<ulong> WaitForSnapPeerLatestAsync(IPeerPool pool, ulong pivotDistance, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var bestPeerLatest = pool.ActivePeers
                    .OfType<SyncPeerSession>()
                    .Where(s => s.PeerLatestBlock > pivotDistance)
                    .Select(s => s.PeerLatestBlock)
                    .DefaultIfEmpty(0UL)
                    .Max();
                if (bestPeerLatest > 0) return bestPeerLatest;
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }

        private static async Task<(BlockHeader Header, byte[] Hash)> FetchPivotWithRetryAsync(
            IPeerPool pool, IFetchRequestScheduler scheduler, ILogger logger, CancellationToken ct,
            ICanonicalStateRootSource canonicalTip, ulong savedPivotFloor = 0,
            IChainStoreBundle bundle = null, bool floorOnReturnedPivot = false)
        {
            int consecutiveFailures = 0;
            int backoffMs = PivotFetchInitialBackoffMs;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                ulong peerLatest;
                if (canonicalTip != null)
                {
                    CanonicalTip tip;
                    try
                    {
                        tip = await canonicalTip.GetLatestAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning(ex, "Snap-bootstrap: canonical tip source {Source} threw; retrying in {BackoffMs}ms", canonicalTip.Name, backoffMs);
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    if (tip == null || tip.BlockNumber <= PivotTrailDistance)
                    {
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    ulong seedTrail = Math.Min(PivotServeTrailDistance, tip.BlockNumber - 1);
                    if (!CanonicalPivotMeetsResumeFloor(tip.BlockNumber - seedTrail, savedPivotFloor))
                    {
                        logger.LogInformation(
                            "snap.bootstrap.pivot_wait trailed={Trailed} < saved_pivot={Floor} — waiting for canonical tip to reach the saved pivot (no regression)",
                            tip.BlockNumber - seedTrail, savedPivotFloor);
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    if (bundle != null)
                    {
                        var trailed = await TrailedPivotFromStoreAsync(bundle, tip, ct).ConfigureAwait(false);
                        if (trailed.HasValue && floorOnReturnedPivot
                            && !CanonicalPivotMeetsResumeFloor((ulong)trailed.Value.Header.BlockNumber, savedPivotFloor))
                        {
                            logger.LogInformation(
                                "snap.bootstrap.pivot_wait trailed_pivot={Trailed} < saved_pivot={Floor} — laid headers lag the tip; waiting so snap/2 never adopts a pivot below its saved one",
                                trailed.Value.Header.BlockNumber, savedPivotFloor);
                            await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                            backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                            continue;
                        }
                        if (trailed.HasValue)
                        {
                            logger.LogInformation(
                                "Snap-bootstrap: pivot anchored on verified local chain block={Block} (tip={Tip}) stateRoot=0x{Root}",
                                trailed.Value.Header.BlockNumber, tip.BlockNumber, trailed.Value.Header.StateRoot.ToHex());
                            return trailed.Value;
                        }
                        logger.LogInformation(
                            "snap.bootstrap.pivot_wait headers not yet laid/verified at tip-{Trail} (tip={Tip}) — waiting for the Headers job",
                            seedTrail, tip.BlockNumber);
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    if (tip.StateRoot != null && tip.StateRoot.Length == 32
                        && tip.BlockHash != null && tip.BlockHash.Length == 32)
                    {
                        logger.LogInformation("Snap-bootstrap: pivot anchored on canonical tip block={Block} stateRoot=0x{Root}", tip.BlockNumber, tip.StateRoot.ToHex());
                        return (new BlockHeader { BlockNumber = tip.BlockNumber, StateRoot = tip.StateRoot }, tip.BlockHash);
                    }
                    peerLatest = tip.BlockNumber;
                }
                else
                {
                    peerLatest = await WaitForSnapPeerLatestAsync(pool, PivotTrailDistance, ct).ConfigureAwait(false);
                }
                var pivotBlock = peerLatest - PivotTrailDistance;

                string failureReason;
                try
                {
                    var headers = await scheduler.FetchHeadersAsync(pivotBlock, limit: 1, ct).ConfigureAwait(false);
                    if (headers != null && headers.Count > 0)
                    {
                        var header = headers[0];
                        return (header, RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header));
                    }
                    failureReason = "empty_response";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failureReason = ex.GetType().Name;
                    logger.LogWarning(ex, "Snap-bootstrap: pivot header fetch threw; will retry");
                }

                consecutiveFailures++;
                if (consecutiveFailures >= PivotFetchMaxConsecutiveFailures)
                {
                    logger.LogWarning("Snap-bootstrap: pivot fetch stalled after {Failures} failures (last: {Reason}); waiting {Delay}ms",
                        consecutiveFailures, failureReason, PivotFetchEscalationDelayMs);
                    await Task.Delay(PivotFetchEscalationDelayMs, ct).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                }
            }
        }
    }
}

