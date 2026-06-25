using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// Hosted-service hook that runs <see cref="SnapBootstrapper.RunAsync"/>
    /// when (a) <see cref="MainnetChainServerConfig.SnapBootstrap"/> is true
    /// and (b) the bundle has no committed state. All snap traffic flows
    /// through the production <see cref="IFetchRequestScheduler"/>, so the
    /// bootstrap inherits the same multi-peer retry / score-based selection /
    /// disconnect tolerance the eth/68 header+body fetches already use.
    ///
    /// <para>
    /// The pool is only consulted to (1) wait until at least one snap-capable
    /// peer exists and (2) sample the highest <c>PeerLatestBlock</c> for
    /// pivot selection. The actual snap dispatch is the scheduler's job —
    /// this hook never binds to one specific peer.
    /// </para>
    /// </summary>
    public static class SnapBootstrapInvoker
    {
        /// <summary>Initial pivot-fetch retry backoff after a transient failure.</summary>
        public const int PivotFetchInitialBackoffMs = 500;

        /// <summary>Cap on the exponential pivot-fetch backoff.</summary>
        public const int PivotFetchMaxBackoffMs = 10_000;

        /// <summary>After this many consecutive pivot-fetch failures the loop logs WARN and sleeps <see cref="PivotFetchEscalationDelayMs"/> between attempts.</summary>
        public const int PivotFetchMaxConsecutiveFailures = 6;

        /// <summary>Long sleep once <see cref="PivotFetchMaxConsecutiveFailures"/> is hit — covers extended peer-starvation without abandoning bootstrap.</summary>
        public const int PivotFetchEscalationDelayMs = 30_000;

        /// <summary>Initial backoff between whole snap-bootstrap attempts. A failed attempt (commonly: no peer can serve state at the pivot root yet) is retried, never fallen back to a genesis replay.</summary>
        public const int SnapAttemptInitialBackoffMs = 2_000;

        /// <summary>Cap on the exponential snap-attempt backoff while waiting for more snap-capable peers.</summary>
        public const int SnapAttemptMaxBackoffMs = 30_000;

        /// <summary>
        /// Trailing distance from network head used to choose the snap pivot block.
        /// The pivot offset is a protocol-level constant, not a deployment knob.
        /// </summary>
        public const ulong PivotTrailDistance = 64;

        public static async Task<SnapBootstrapper.Result> RunIfConfiguredAsync(
            IChainStoreBundle bundle,
            IPeerPool? pool,
            IFetchRequestScheduler? scheduler,
            MainnetChainServerConfig config,
            ILogger logger,
            CancellationToken ct,
            ICanonicalStateRootSource? canonicalTip = null,
            SnapSyncMetrics? metrics = null)
        {
            if (!config.SnapBootstrap)
            {
                logger.LogInformation(
                    "snap.bootstrap.skip reason=config_disabled");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "SnapBootstrap config flag is false" };
            }

            var lastBlock = bundle.Metadata.GetLastBlock();
            if (lastBlock > 0)
            {
                logger.LogInformation(
                    "snap.bootstrap.skip reason=committed_state block={Block}",
                    lastBlock);
                return new SnapBootstrapper.Result { Ran = false, SkipReason = $"existing state at block {lastBlock}" };
            }

            if (pool == null || scheduler == null)
            {
                logger.LogInformation(
                    "snap.bootstrap.skip reason=no_pool_or_scheduler");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "no peer pool / scheduler registered (in-memory composition?)" };
            }

            var peerCount = pool.ActivePeers.Count;
            var trustedSource = canonicalTip?.Name ?? "<peer-pool-sampling>";
            logger.LogInformation(
                "snap.bootstrap.entry peer_count={PeerCount} trusted_source={TrustedSource}",
                peerCount, trustedSource);

            // A snap node NEVER falls back to a genesis full-replay. If an attempt
            // cannot complete — the common case being that no currently-connected
            // peer can serve state at the pivot root yet — we retry with backoff as
            // discovery brings more snap-capable peers online. Partial progress is
            // persisted (Phase2Running + cursors) and resumed on each attempt.
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
                        pool, scheduler, config, logger, ct, canonicalTip).ConfigureAwait(false);
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

                    // SchedulerSnapPeer routes every snap request through the
                    // same scheduler used for headers/bodies — multi-peer,
                    // retry on disconnect, snap-capable filter applied
                    // upstream. SnapBootstrapper sees a plain ISnapPeer.
                    var snapPeer = new SchedulerSnapPeer(scheduler);

                    // Pivot refresher: samples the highest peer_latest across
                    // ALL eth peers (pivot = networkHead - pivot distance,
                    // independent of snap capability), recomputes pivot block,
                    // fetches the fresh header via scheduler. Returns null on any
                    // failure — caller continues against current root.
                    Func<CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher =
                        async refreshCt =>
                    {
                        try
                        {
                            ulong currentTipBlock;
                            if (canonicalTip != null)
                            {
                                var tip = await canonicalTip.GetLatestAsync(refreshCt).ConfigureAwait(false);
                                if (tip == null || tip.BlockNumber <= PivotTrailDistance) return null;
                                // Same as the initial pivot pick: anchor directly on the
                                // canonical tip (StateRoot + BlockHash). The walker lays +
                                // verifies the full header; no by-number re-fetch.
                                if (tip.StateRoot != null && tip.StateRoot.Length == 32
                                    && tip.BlockHash != null && tip.BlockHash.Length == 32)
                                {
                                    return (new BlockHeader
                                    {
                                        BlockNumber = tip.BlockNumber,
                                        StateRoot = tip.StateRoot,
                                    }, tip.BlockHash);
                                }
                                currentTipBlock = tip.BlockNumber;
                            }
                            else
                            {
                                var currentPeerLatest = pool.ActivePeers
                                    .OfType<MainnetPeerSession>()
                                    .Where(s => s.PeerLatestBlock > PivotTrailDistance)
                                    .Select(s => s.PeerLatestBlock)
                                    .DefaultIfEmpty(0UL)
                                    .Max();
                                if (currentPeerLatest == 0) return null;
                                currentTipBlock = currentPeerLatest;
                            }
                            var newPivotBlock = currentTipBlock - PivotTrailDistance;
                            var newHeaders = await scheduler.FetchHeadersAsync(
                                newPivotBlock, limit: 1, refreshCt).ConfigureAwait(false);
                            if (newHeaders == null || newHeaders.Count == 0) return null;
                            var newHeader = newHeaders[0];
                            var newHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(newHeader);
                            return (newHeader, newHash);
                        }
                        catch { return null; }
                    };

                    var result = await SnapBootstrapper.RunAsync(
                        bundle, snapPeer, pivotHeader, pivotHash, logger, scheduler, refresher,
                        runBackfill: true,
                        activations: MainnetChainActivations.Instance, pool: pool, metrics: metrics,
                        useBackwardSkeleton: config.BackwardSkeletonPhase1, ct: ct).ConfigureAwait(false);

                    logger.LogInformation(
                        "Snap-bootstrap: completed at pivot block {Block} ({Accounts} accounts, {Slots} slots, {Codes} bytecodes).",
                        result.PivotBlockNumber, result.AccountCount, result.SlotCount, result.BytecodeCount);
                    return result;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "cancelled during snap-bootstrap" };
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex,
                        "snap.bootstrap.retry attempt={Attempt} reason={Reason}; retrying in {BackoffMs}ms as more snap-capable peers are discovered (no genesis fallback)",
                        attempt, ex.GetType().Name, retryBackoffMs);
                    await Task.Delay(retryBackoffMs, ct).ConfigureAwait(false);
                    retryBackoffMs = System.Math.Min(retryBackoffMs * 2, SnapAttemptMaxBackoffMs);
                }
            }
        }

        // Samples the highest peer_latest across ALL eth peers (not filtered
        // by snap capability): pivot block is derived from the network head,
        // not from snap-server availability. Requiring snap support here meant
        // a sole stale peer set the pivot at its (stale) head, leaving
        // every snap query empty.
        private static async Task<ulong> WaitForSnapPeerLatestAsync(
            IPeerPool pool, ulong pivotDistance, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var bestPeerLatest = pool.ActivePeers
                    .OfType<MainnetPeerSession>()
                    .Where(s => s.PeerLatestBlock > pivotDistance)
                    .Select(s => s.PeerLatestBlock)
                    .DefaultIfEmpty(0UL)
                    .Max();

                if (bestPeerLatest > 0) return bestPeerLatest;
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }

        // Resolve the full pivot header by its trusted hash and verify it. A peer
        // still syncing serves canonical-only by NUMBER (empty for a recent block) but
        // serves any block it holds BY HASH, so this works where a by-number pivot
        // fetch stalls. The header is rejected unless it hashes to the tip's BlockHash
        // AND matches the tip's BlockNumber + StateRoot — guaranteeing the persisted
        // pivot is a full, cryptographically-verified header (never a partial one).
        // Returns null when no peer served it yet or the response failed verification.
        private static async Task<(BlockHeader Header, byte[] Hash)?> ResolvePivotHeaderByHashAsync(
            IFetchRequestScheduler scheduler, CanonicalTip tip, ILogger logger, CancellationToken ct)
        {
            List<BlockHeader> byHash;
            try
            {
                byHash = await scheduler.FetchHeadersByHashAsync(tip.BlockHash, limit: 1, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Snap-bootstrap: pivot header-by-hash fetch threw; will retry");
                return null;
            }
            if (byHash == null || byHash.Count == 0)
                return null;

            var full = byHash[0];
            var computed = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(full);
            if (computed == null || !computed.AsSpan().SequenceEqual(tip.BlockHash))
            {
                logger.LogWarning(
                    "Snap-bootstrap: pivot header-by-hash REJECTED — peer header does not hash to the trusted tip hash 0x{Hash}",
                    tip.BlockHash.ToHex());
                return null;
            }
            if ((ulong)full.BlockNumber != tip.BlockNumber
                || full.StateRoot == null || !full.StateRoot.AsSpan().SequenceEqual(tip.StateRoot))
            {
                logger.LogWarning(
                    "Snap-bootstrap: pivot header-by-hash REJECTED — number/stateRoot disagree with the trusted tip (block={Block})",
                    tip.BlockNumber);
                return null;
            }

            logger.LogInformation(
                "Snap-bootstrap: pivot header resolved by hash + verified against canonical tip block={Block} stateRoot=0x{Root}",
                tip.BlockNumber, tip.StateRoot.ToHex());
            return (full, tip.BlockHash);
        }

        // Re-evaluates the snap-capable peer pool and re-fetches the pivot
        // header on every attempt — peer pool churn between attempts is the
        // common failure mode, not a permanent error. Bounded only by ct.
        private static async Task<(BlockHeader Header, byte[] Hash)> FetchPivotWithRetryAsync(
            IPeerPool pool,
            IFetchRequestScheduler scheduler,
            MainnetChainServerConfig config,
            ILogger logger,
            CancellationToken ct,
            ICanonicalStateRootSource? canonicalTip)
        {
            if (canonicalTip != null)
            {
                logger.LogInformation(
                    "Snap-bootstrap: canonical tip source registered ({Source}); pivot block = canonical_tip - {Distance}",
                    canonicalTip.Name, PivotTrailDistance);
            }
            else
            {
                logger.LogInformation(
                    "Snap-bootstrap: no canonical tip source; falling back to peer-pool max sampling. Waiting for any eth peer with peer_latest > pivot_distance={Distance} (sampled across ALL peers, not just snap-capable)...",
                    PivotTrailDistance);
            }

            int consecutiveFailures = 0;
            int backoffMs = PivotFetchInitialBackoffMs;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                ulong peerLatest;
                if (canonicalTip != null)
                {
                    CanonicalTip tip = null;
                    try
                    {
                        tip = await canonicalTip.GetLatestAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning(ex,
                            "Snap-bootstrap: canonical tip source {Source} threw; retrying in {BackoffMs}ms",
                            canonicalTip.Name, backoffMs);
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    if (tip == null || tip.BlockNumber <= PivotTrailDistance)
                    {
                        logger.LogInformation(
                            "Snap-bootstrap: canonical tip source {Source} returned no tip yet; retrying in {BackoffMs}ms",
                            canonicalTip.Name, backoffMs);
                        await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                        backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                        continue;
                    }
                    // The canonical (light-client) tip carries the verified BlockHash +
                    // StateRoot. Use them directly as the pivot anchor: StateRoot drives
                    // Phase 2, BlockHash is the trusted anchor the backward header walker
                    // validates against (it fetches + hash-verifies + lays the FULL pivot
                    // header as the top of its first batch). No trail subtraction and no
                    // by-number re-fetch (the path that stalled the bootstrap).
                    if (tip.StateRoot != null && tip.StateRoot.Length == 32
                        && tip.BlockHash != null && tip.BlockHash.Length == 32)
                    {
                        logger.LogInformation(
                            "Snap-bootstrap: pivot anchored on canonical tip block={Block} stateRoot=0x{Root} (full header laid + verified by the backward walker)",
                            tip.BlockNumber, tip.StateRoot.ToHex());
                        var pivotFromTip = new BlockHeader
                        {
                            BlockNumber = tip.BlockNumber,
                            StateRoot = tip.StateRoot,
                        };
                        return (pivotFromTip, tip.BlockHash);
                    }

                    peerLatest = tip.BlockNumber;
                }
                else
                {
                    peerLatest = await WaitForSnapPeerLatestAsync(pool, PivotTrailDistance, ct)
                        .ConfigureAwait(false);
                }
                var pivotBlock = peerLatest - PivotTrailDistance;

                logger.LogInformation(
                    "Snap-bootstrap: peer_latest_sampled={PeerLatest}, pivot_block={PivotBlock}; fetching pivot header via scheduler...",
                    peerLatest, pivotBlock);

                string failureReason = null;
                try
                {
                    var headers = await scheduler.FetchHeadersAsync(pivotBlock, limit: 1, ct).ConfigureAwait(false);
                    if (headers != null && headers.Count > 0)
                    {
                        var header = headers[0];
                        var hash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
                        return (header, hash);
                    }
                    failureReason = "empty_response";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failureReason = ex.GetType().Name;
                    logger.LogWarning(ex,
                        "Snap-bootstrap: pivot header fetch threw; will retry");
                }

                consecutiveFailures++;
                if (consecutiveFailures >= PivotFetchMaxConsecutiveFailures)
                {
                    logger.LogWarning(
                        "Snap-bootstrap: pivot fetch stalled after {Failures} consecutive failures (last reason: {Reason}); waiting {Delay}ms before retry",
                        consecutiveFailures, failureReason, PivotFetchEscalationDelayMs);
                    await Task.Delay(PivotFetchEscalationDelayMs, ct).ConfigureAwait(false);
                }
                else
                {
                    logger.LogInformation(
                        "Snap-bootstrap: pivot fetch failed ({Reason}); retrying in {BackoffMs}ms",
                        failureReason, backoffMs);
                    await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, PivotFetchMaxBackoffMs);
                }
            }
        }
    }
}
