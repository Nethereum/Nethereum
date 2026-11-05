using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapSyncOrchestrator;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class PivotAcquisition
    {
        private readonly IPeerPool _pool;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly ILogger _logger;
        private readonly ICanonicalStateRootSource _canonicalTip;
        private readonly IChainStoreBundle _bundle;
        private readonly ulong _staleDistanceBlocks;
        private int _consecutiveFailures;
        private readonly Backoff _backoff = new Backoff(PivotFetchInitialBackoffMs);

        private readonly record struct AnchorOutcome((BlockHeader Header, byte[] Hash)? Pivot, ulong PeerLatest, bool Wait);

        internal PivotAcquisition(
            IPeerPool pool, IFetchRequestScheduler scheduler, ILogger logger, ICanonicalStateRootSource canonicalTip,
            IChainStoreBundle bundle, ulong staleDistanceBlocks)
        {
            _pool = pool;
            _scheduler = scheduler;
            _logger = logger;
            _canonicalTip = canonicalTip;
            _bundle = bundle;
            _staleDistanceBlocks = staleDistanceBlocks;
        }

        internal async Task<(BlockHeader Header, byte[] Hash)> FetchAsync(ulong savedPivotFloor, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var anchor = _canonicalTip != null
                    ? await TryAnchorOnCanonicalTipAsync(savedPivotFloor, ct).ConfigureAwait(false)
                    : new AnchorOutcome(null, await TrailedPivot.WaitForSnapPeerLatestAsync(_pool, PivotTrailDistance, ct).ConfigureAwait(false), false);
                if (anchor.Pivot.HasValue) return anchor.Pivot.Value;
                if (anchor.Wait)
                {
                    await BackOffAsync(ct).ConfigureAwait(false);
                    continue;
                }

                var fetched = await FetchHeaderFromPeersAsync(anchor.PeerLatest - PivotTrailDistance, ct).ConfigureAwait(false);
                if (fetched.HasValue) return fetched.Value;
            }
        }

        private async Task<AnchorOutcome> TryAnchorOnCanonicalTipAsync(ulong savedPivotFloor, CancellationToken ct)
        {
            var tip = await ReadTipAsync(ct).ConfigureAwait(false);
            if (tip == null || tip.BlockNumber <= PivotTrailDistance)
                return new AnchorOutcome(null, 0, true);
            ulong seedTrail = Math.Min(PivotServeTrailDistance, tip.BlockNumber - 1);
            if (!CanonicalPivotMeetsResumeFloor(tip.BlockNumber - seedTrail, savedPivotFloor))
            {
                _logger.LogInformation(
                    "snap.bootstrap.pivot_wait trailed={Trailed} < saved_pivot={Floor} — waiting for canonical tip to reach the saved pivot (no regression)",
                    tip.BlockNumber - seedTrail, savedPivotFloor);
                return new AnchorOutcome(null, 0, true);
            }
            if (_bundle != null)
                return await AnchorOnLaidHeadersAsync(tip, seedTrail, savedPivotFloor, ct).ConfigureAwait(false);
            if (TrailedPivot.HasPivotFields(tip))
            {
                _logger.LogInformation("Snap-bootstrap: pivot anchored on canonical tip block={Block} stateRoot=0x{Root}", tip.BlockNumber, tip.StateRoot.ToHex());
                return new AnchorOutcome((new BlockHeader { BlockNumber = tip.BlockNumber, StateRoot = tip.StateRoot }, tip.BlockHash), tip.BlockNumber, false);
            }
            return new AnchorOutcome(null, tip.BlockNumber, false);
        }

        private async Task<CanonicalTip> ReadTipAsync(CancellationToken ct)
        {
            try
            {
                return await _canonicalTip.GetLatestAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Snap-bootstrap: canonical tip source {Source} threw; retrying in {BackoffMs}ms", _canonicalTip.Name, _backoff.Current);
                return null;
            }
        }

        private async Task<AnchorOutcome> AnchorOnLaidHeadersAsync(
            CanonicalTip tip, ulong seedTrail, ulong savedPivotFloor, CancellationToken ct)
        {
            var fresh = await TrailedPivot.FreshTrailedPivotAsync(_bundle, tip, savedPivotFloor, _staleDistanceBlocks, ct).ConfigureAwait(false);
            if (fresh.Wait == TrailedPivot.FreshPivotWait.LaidHeadersLag)
            {
                _logger.LogInformation(
                    "snap.bootstrap.pivot_wait trailed_pivot={Trailed} < floor={Floor} (saved_pivot={Saved} tip={Tip} laid_tip={Laid}) — laid headers lag; waiting for the Headers job",
                    fresh.TrailedBlock, fresh.Floor, savedPivotFloor, tip.BlockNumber,
                    HeaderSubchains.TrustedTip(_bundle.Metadata.GetHeaderSyncState()));
                return new AnchorOutcome(null, 0, true);
            }
            if (fresh.Wait == TrailedPivot.FreshPivotWait.None)
            {
                _logger.LogInformation(
                    "Snap-bootstrap: pivot anchored on verified local chain block={Block} (tip={Tip}) stateRoot=0x{Root}",
                    fresh.TrailedBlock, tip.BlockNumber, fresh.Pivot.Value.Header.StateRoot.ToHex());
                return new AnchorOutcome(fresh.Pivot, tip.BlockNumber, false);
            }
            _logger.LogInformation(
                "snap.bootstrap.pivot_wait headers not yet laid/verified at tip-{Trail} (tip={Tip}) — waiting for the Headers job",
                seedTrail, tip.BlockNumber);
            return new AnchorOutcome(null, 0, true);
        }

        private async Task<(BlockHeader Header, byte[] Hash)?> FetchHeaderFromPeersAsync(ulong pivotBlock, CancellationToken ct)
        {
            string failureReason;
            try
            {
                var headers = await _scheduler.FetchHeadersAsync(pivotBlock, limit: 1, ct).ConfigureAwait(false);
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
                _logger.LogWarning(ex, "Snap-bootstrap: pivot header fetch threw; will retry");
            }

            await OnFetchFailedAsync(failureReason, ct).ConfigureAwait(false);
            return null;
        }

        private async Task OnFetchFailedAsync(string failureReason, CancellationToken ct)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= PivotFetchMaxConsecutiveFailures)
            {
                _logger.LogWarning("Snap-bootstrap: pivot fetch stalled after {Failures} failures (last: {Reason}); waiting {Delay}ms",
                    _consecutiveFailures, failureReason, PivotFetchEscalationDelayMs);
                await Task.Delay(PivotFetchEscalationDelayMs, ct).ConfigureAwait(false);
            }
            else
            {
                await BackOffAsync(ct).ConfigureAwait(false);
            }
        }

        private async Task BackOffAsync(CancellationToken ct)
        {
            await Task.Delay(_backoff.Current, ct).ConfigureAwait(false);
            _backoff.Advance(PivotFetchMaxBackoffMs);
        }
    }
}
