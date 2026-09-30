using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    public sealed class HeaderFollowService
    {
        private readonly ICanonicalStateRootSource _canonical;
        private readonly BackwardWalkerDelegate _walker;
        private readonly AncestorResolverDelegate _ancestorResolver;
        private readonly HeaderFollowOptions _options;
        private readonly ILogger _logger;

        private readonly SemaphoreSlim _skeletonLock = new(1, 1);

        public HeaderFollowService(
            ICanonicalStateRootSource canonical,
            BackwardWalkerDelegate walker,
            HeaderFollowOptions options = null,
            ILogger logger = null,
            AncestorResolverDelegate ancestorResolver = null)
        {
            _canonical = canonical ?? throw new ArgumentNullException(nameof(canonical));
            _walker = walker ?? throw new ArgumentNullException(nameof(walker));
            _options = options ?? new HeaderFollowOptions();
            _logger = logger ?? NullLogger.Instance;
            _ancestorResolver = ancestorResolver;
        }

        public async Task RunAsync(IChainStoreBundle bundle, CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var tipTask = Task.Run(() => TipFollowLoopAsync(bundle, ct), ct);
            var descentTask = Task.Run(() => DescentLoopAsync(bundle, ct), ct);
            await Task.WhenAll(tipTask, descentTask).ConfigureAwait(false);
        }

        private async Task TipFollowLoopAsync(IChainStoreBundle bundle, CancellationToken ct)
        {
            ulong lastSeenTipBlock = 0UL;
            byte[] lastSeenTipHash = null;
            int consecutiveSourceFailures = 0;

            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                var cycle = await AdvanceOnceAsync(bundle, lastSeenTipBlock, lastSeenTipHash, ct)
                    .ConfigureAwait(false);

                switch (cycle.Status)
                {
                    case HeaderFollowStatus.Advanced:
                        lastSeenTipBlock = cycle.TipBlock;
                        lastSeenTipHash = cycle.TipHash;
                        consecutiveSourceFailures = 0;
                        break;

                    case HeaderFollowStatus.NotAdvanced:
                        consecutiveSourceFailures = 0;
                        await Task.Delay(_options.PollInterval, ct).ConfigureAwait(false);
                        break;

                    case HeaderFollowStatus.SourceUnavailable:
                        consecutiveSourceFailures++;
                        if (consecutiveSourceFailures % _options.SourceFailureLogEvery == 0)
                            _logger.LogError(
                                "snap.headers.source_unavailable source={Source} consecutive={Failures} — still retrying",
                                _canonical.Name, consecutiveSourceFailures);
                        await Task.Delay(_options.PollInterval, ct).ConfigureAwait(false);
                        break;

                    case HeaderFollowStatus.PeerPoolEmpty:
                    case HeaderFollowStatus.WalkFailed:
                        lastSeenTipHash = null;
                        await Task.Delay(_options.PollInterval, ct).ConfigureAwait(false);
                        break;

                    case HeaderFollowStatus.Divergence:
                        _logger.LogWarning(
                            "snap.headers.divergence at block {Block}; no ancestor resolver wired — backing off",
                            cycle.DivergenceBlock);
                        lastSeenTipHash = null;
                        await Task.Delay(_options.PollInterval, ct).ConfigureAwait(false);
                        break;
                }
            }
        }

        public async Task<HeaderFollowCycle> AdvanceOnceAsync(
            IChainStoreBundle bundle,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var tip = await PollCanonicalTipAsync(ct).ConfigureAwait(false);
            if (tip == null)
                return HeaderFollowCycle.SourceUnavailable();

            var top = HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState());
            bool sameAsLastSeen = IsSameTipAsLastSeen(tip, lastSeenTipBlock, lastSeenTipHash);
            if (tip.BlockNumber <= top || sameAsLastSeen)
                return HeaderFollowCycle.NotAdvanced();

            ulong target = BoundedTipWalkTarget(tip, top);

            var walk = await WalkFromTipAsync(bundle, tip, target, ct).ConfigureAwait(false);
            if (walk.Threw)
                return HeaderFollowCycle.WalkFailed();
            var outcome = walk.Outcome;

            switch (outcome.ExitReason)
            {
                case WalkerExitReason.PeerPoolEmpty:
                    await RecordWalkAsync(bundle, tip.BlockNumber, outcome, ct).ConfigureAwait(false);
                    return HeaderFollowCycle.PeerPoolEmpty();

                case WalkerExitReason.Cancelled:
                    throw new OperationCanceledException(ct);

                case WalkerExitReason.LastKnownGoodDivergence:
                    if (_ancestorResolver == null)
                        return HeaderFollowCycle.Divergence(outcome.DivergenceBlock);
                    return await RepairDivergenceAsync(bundle, tip, outcome.DivergenceBlock.Value, ct)
                        .ConfigureAwait(false);

                case WalkerExitReason.MetExistingStore:
                case WalkerExitReason.ReachedTarget:
                case WalkerExitReason.StructuralGenesis:
                    break;
            }

            await RecordWalkAsync(bundle, tip.BlockNumber, outcome, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "snap.headers.followed tip={Tip} bottom={Bottom} headers_written={Written} linked={Linked}",
                tip.BlockNumber, outcome.SkeletonBottomBlock, outcome.HeadersWritten, outcome.MetExistingStore);

            return HeaderFollowCycle.Advanced(tip.BlockNumber, tip.BlockHash, outcome);
        }

        private async Task<CanonicalTip> PollCanonicalTipAsync(CancellationToken ct)
        {
            CanonicalTip tip;
            try
            {
                tip = await _canonical.GetLatestAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "snap.headers.poll_failed source={Source}", _canonical.Name);
                return null;
            }

            return tip;
        }

        private static bool IsSameTipAsLastSeen(CanonicalTip tip, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
        {
            bool sameAsLastSeen = lastSeenTipHash != null && tip.BlockHash != null
                && ByteUtil.AreEqual(tip.BlockHash, lastSeenTipHash) && tip.BlockNumber == lastSeenTipBlock;
            return sameAsLastSeen;
        }

        private ulong BoundedTipWalkTarget(CanonicalTip tip, ulong top)
        {
            ulong gap = tip.BlockNumber - top;
            ulong target = gap > _options.MaxTipWalkBlocks
                ? tip.BlockNumber - _options.MaxTipWalkBlocks
                : top;
            return target;
        }

        private async Task<(bool Threw, WalkerOutcome Outcome)> WalkFromTipAsync(
            IChainStoreBundle bundle, CanonicalTip tip, ulong target, CancellationToken ct)
        {
            WalkerOutcome outcome;
            try
            {
                outcome = await _walker(tip.BlockNumber, tip.BlockHash, target, bundle, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "snap.headers.walk_exception tip={Tip}", tip.BlockNumber);
                return (true, null);
            }

            return (false, outcome);
        }

        private async Task<HeaderFollowCycle> RepairDivergenceAsync(
            IChainStoreBundle bundle, CanonicalTip tip, ulong divergedBlock, CancellationToken ct)
        {
            for (int attempt = 0; attempt < _options.MaxDivergenceRepairAttempts; attempt++)
            {
                ulong floor = divergedBlock > _options.ReorgSearchDepth
                    ? divergedBlock - _options.ReorgSearchDepth
                    : 0UL;

                ulong ancestor;
                try
                {
                    ancestor = await _ancestorResolver(divergedBlock, floor, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "snap.headers.ancestor_failed diverged={Diverged} floor={Floor} — backing off",
                        divergedBlock, floor);
                    return HeaderFollowCycle.Divergence(divergedBlock);
                }

                WalkerOutcome relay;
                try
                {
                    relay = await _walker(tip.BlockNumber, tip.BlockHash, ancestor, bundle, ct,
                        noShortCircuitAboveBlock: ancestor).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "snap.headers.relay_failed range=({Ancestor}..{Tip}] — retrying next poll",
                        ancestor, tip.BlockNumber);
                    return HeaderFollowCycle.Divergence(divergedBlock);
                }

                if (relay.ExitReason == WalkerExitReason.LastKnownGoodDivergence
                    && relay.DivergenceBlock != null)
                {
                    _logger.LogWarning(
                        "snap.headers.relay_diverged at={Block} ancestor={Ancestor} — re-resolving deeper",
                        relay.DivergenceBlock, ancestor);
                    divergedBlock = relay.DivergenceBlock.Value;
                    continue;
                }

                await RecordWalkAsync(bundle, tip.BlockNumber, relay, ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "snap.headers.reorg_repaired ancestor={Ancestor} tip={Tip} headers={Headers}",
                    ancestor, tip.BlockNumber, relay.HeadersWritten);
                return HeaderFollowCycle.Advanced(tip.BlockNumber, tip.BlockHash, relay);
            }

            _logger.LogError(
                "snap.headers.repair_exhausted diverged={Diverged} after={Attempts} attempts",
                divergedBlock, _options.MaxDivergenceRepairAttempts);
            return HeaderFollowCycle.Divergence(divergedBlock);
        }

        private async Task DescentLoopAsync(IChainStoreBundle bundle, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                bool progressed;
                try
                {
                    progressed = await DescendOnceAsync(bundle, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "snap.headers.descent_failed — retrying");
                    progressed = false;
                }

                if (!progressed)
                    await Task.Delay(_options.DescentIdleInterval, ct).ConfigureAwait(false);
            }
        }

        public async Task<bool> DescendOnceAsync(IChainStoreBundle bundle, CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var state = bundle.Metadata.GetHeaderSyncState();
            var subchains = state.Subchains;
            if (subchains.Count == 0) return false;

            var (open, gapFloor) = FindHighestOpenGap(subchains);
            if (open == null) return false;

            ulong resumeFrom = await ResolveResumePointAsync(bundle, open, gapFloor).ConfigureAwait(false);

            var anchorHash = await ResolveDescentAnchorAsync(bundle, open, resumeFrom).ConfigureAwait(false);
            if (anchorHash == null) return false;

            ulong chunkBottom = BoundedChunkBottom(resumeFrom, gapFloor);

            var walk = await WalkDescentChunkAsync(bundle, resumeFrom, anchorHash, chunkBottom, ct)
                .ConfigureAwait(false);
            if (walk.Threw) return false;
            var outcome = walk.Outcome;

            switch (outcome.ExitReason)
            {
                case WalkerExitReason.PeerPoolEmpty:
                    await RecordDescentAsync(bundle, open.Head, outcome, ct).ConfigureAwait(false);
                    return outcome.HeadersWritten > 0;

                case WalkerExitReason.Cancelled:
                    throw new OperationCanceledException(ct);

                case WalkerExitReason.LastKnownGoodDivergence:
                    return await OverwriteStaleDescentRowAsync(
                        bundle, open, resumeFrom, anchorHash, chunkBottom, outcome, ct).ConfigureAwait(false);

                case WalkerExitReason.MetExistingStore:
                case WalkerExitReason.ReachedTarget:
                case WalkerExitReason.StructuralGenesis:
                    break;
            }

            await RecordDescentAsync(bundle, open.Head, outcome, ct).ConfigureAwait(false);

            if (DescentLinkedToNextSubchain(outcome, gapFloor))
                _logger.LogInformation(
                    "snap.headers.descent_linked subchain_head={Head} bottom={Bottom}",
                    open.Head, outcome.SkeletonBottomBlock);

            return true;
        }

        private static (HeaderSubchain Open, ulong GapFloor) FindHighestOpenGap(
            IReadOnlyList<HeaderSubchain> subchains)
        {
            HeaderSubchain open = null;
            ulong gapFloor = 0;
            for (int i = 0; i < subchains.Count; i++)
            {
                ulong floorBelow = i + 1 < subchains.Count ? subchains[i + 1].Head + 1 : 0UL;
                if (subchains[i].Tail > floorBelow)
                {
                    open = subchains[i];
                    gapFloor = floorBelow;
                    break;
                }
            }
            return (open, gapFloor);
        }

        private async Task<ulong> ResolveResumePointAsync(
            IChainStoreBundle bundle, HeaderSubchain open, ulong gapFloor)
        {
            ulong resumeFrom = open.Tail;
            var cursor = bundle.Metadata.GetLastFetchedHeader();
            if (cursor > gapFloor && cursor < open.Tail)
            {
                var cursorHash = await bundle.Blocks.GetHashByNumberAsync(cursor).ConfigureAwait(false);
                if (cursorHash != null)
                    resumeFrom = cursor;
                else
                    _logger.LogError(
                        "snap.headers.cursor_stale cursor={Cursor} points at a missing header — resuming from recorded tail {Tail}",
                        cursor, open.Tail);
            }
            return resumeFrom;
        }

        private async Task<byte[]> ResolveDescentAnchorAsync(
            IChainStoreBundle bundle, HeaderSubchain open, ulong resumeFrom)
        {
            var anchorHash = await bundle.Blocks.GetHashByNumberAsync(resumeFrom).ConfigureAwait(false);
            if (anchorHash == null)
            {
                _logger.LogError(
                    "snap.headers.descent_anchor_missing block={Block} — subchain [{Tail}..{Head}] cannot descend",
                    resumeFrom, open.Tail, open.Head);
                return null;
            }

            return anchorHash;
        }

        private ulong BoundedChunkBottom(ulong resumeFrom, ulong gapFloor)
        {
            ulong chunkBottom = resumeFrom - gapFloor > _options.DescentChunkBlocks
                ? resumeFrom - _options.DescentChunkBlocks
                : gapFloor;
            return chunkBottom;
        }

        private async Task<(bool Threw, WalkerOutcome Outcome)> WalkDescentChunkAsync(
            IChainStoreBundle bundle, ulong resumeFrom, byte[] anchorHash, ulong chunkBottom, CancellationToken ct)
        {
            WalkerOutcome outcome;
            try
            {
                outcome = await _walker(resumeFrom, anchorHash, chunkBottom, bundle, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "snap.headers.descent_walk_failed from={From} to={To}", resumeFrom, chunkBottom);
                return (true, null);
            }

            return (false, outcome);
        }

        private async Task<bool> OverwriteStaleDescentRowAsync(
            IChainStoreBundle bundle, HeaderSubchain open, ulong resumeFrom, byte[] anchorHash,
            ulong chunkBottom, WalkerOutcome outcome, CancellationToken ct)
        {
            _logger.LogWarning(
                "snap.headers.descent_divergence at={Block} — overwriting the stale row and continuing",
                outcome.DivergenceBlock);
            try
            {
                var relay = await _walker(resumeFrom, anchorHash, chunkBottom, bundle, ct,
                    noShortCircuitAboveBlock: chunkBottom).ConfigureAwait(false);
                await RecordDescentAsync(bundle, open.Head, relay, ct).ConfigureAwait(false);
                return relay.HeadersWritten > 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "snap.headers.descent_relay_failed at={Block}", outcome.DivergenceBlock);
                return false;
            }
        }

        private static bool DescentLinkedToNextSubchain(WalkerOutcome outcome, ulong gapFloor)
            => outcome.SkeletonBottomBlock == 0 || outcome.SkeletonBottomBlock <= gapFloor;

        private async Task RecordWalkAsync(
            IChainStoreBundle bundle, ulong tipBlock, WalkerOutcome outcome, CancellationToken ct)
        {
            if (outcome.HeadersWritten == 0) return;
            await _skeletonLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var hss = bundle.Metadata.GetHeaderSyncState();
                hss = HeaderSubchains.OpenTip(hss, tipBlock);
                hss = HeaderSubchains.RecordDescent(hss, tipBlock, outcome.SkeletonBottomBlock);
                bundle.Metadata.SaveHeaderSyncState(hss);
            }
            finally { _skeletonLock.Release(); }
        }

        private async Task RecordDescentAsync(
            IChainStoreBundle bundle, ulong subchainHead, WalkerOutcome outcome, CancellationToken ct)
        {
            if (outcome.HeadersWritten == 0) return;
            await _skeletonLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var hss = bundle.Metadata.GetHeaderSyncState();
                hss = HeaderSubchains.RecordDescent(hss, subchainHead, outcome.SkeletonBottomBlock);
                bundle.Metadata.SaveHeaderSyncState(hss);
            }
            finally { _skeletonLock.Release(); }
        }
    }

    public enum HeaderFollowStatus
    {
        Advanced,
        NotAdvanced,
        SourceUnavailable,
        PeerPoolEmpty,
        WalkFailed,
        Divergence,
    }

    public sealed record HeaderFollowCycle(
        HeaderFollowStatus Status,
        ulong TipBlock,
        byte[] TipHash,
        WalkerOutcome Outcome,
        ulong? DivergenceBlock)
    {
        public static HeaderFollowCycle Advanced(ulong tipBlock, byte[] tipHash, WalkerOutcome outcome)
            => new(HeaderFollowStatus.Advanced, tipBlock, tipHash, outcome, null);
        public static HeaderFollowCycle NotAdvanced()
            => new(HeaderFollowStatus.NotAdvanced, 0, null, null, null);
        public static HeaderFollowCycle SourceUnavailable()
            => new(HeaderFollowStatus.SourceUnavailable, 0, null, null, null);
        public static HeaderFollowCycle PeerPoolEmpty()
            => new(HeaderFollowStatus.PeerPoolEmpty, 0, null, null, null);
        public static HeaderFollowCycle WalkFailed()
            => new(HeaderFollowStatus.WalkFailed, 0, null, null, null);
        public static HeaderFollowCycle Divergence(ulong? divergenceBlock)
            => new(HeaderFollowStatus.Divergence, 0, null, null, divergenceBlock);
    }

    public sealed class HeaderFollowOptions
    {
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(12);

        public TimeSpan DescentIdleInterval { get; set; } = TimeSpan.FromSeconds(12);

        public ulong MaxTipWalkBlocks { get; set; } = 4_096;

        public ulong DescentChunkBlocks { get; set; } = 16_384;

        public ulong ReorgSearchDepth { get; set; } = 1_024;

        public int MaxDivergenceRepairAttempts { get; set; } = 3;

        public int SourceFailureLogEvery { get; set; } = 50;
    }
}
