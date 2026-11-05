using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using static Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapSyncOrchestrator;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class PivotRefresh
    {
        private readonly IPeerPool _pool;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly ICanonicalStateRootSource _canonicalTip;
        private readonly ILogger _logger;
        private readonly SnapBootstrapper.RollingPivot _rollingPivot;
        private readonly IChainStoreBundle _bundle;
        private readonly ulong _staleDistanceBlocks;

        internal PivotRefresh(
            IPeerPool pool, IFetchRequestScheduler scheduler, ICanonicalStateRootSource canonicalTip, ILogger logger,
            SnapBootstrapper.RollingPivot rollingPivot, IChainStoreBundle bundle, ulong staleDistanceBlocks)
        {
            _pool = pool;
            _scheduler = scheduler;
            _canonicalTip = canonicalTip;
            _logger = logger;
            _rollingPivot = rollingPivot;
            _bundle = bundle;
            _staleDistanceBlocks = staleDistanceBlocks;
        }

        internal static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> Build(
            IPeerPool pool, IFetchRequestScheduler scheduler, ICanonicalStateRootSource canonicalTip, ILogger logger,
            SnapBootstrapper.RollingPivot rollingPivot, IChainStoreBundle bundle = null,
            ulong staleDistanceBlocks = PivotStaleDistanceBlocks)
            => new PivotRefresh(pool, scheduler, canonicalTip, logger, rollingPivot, bundle, staleDistanceBlocks).AsDelegate();

        internal Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> AsDelegate() => RefreshAsync;

        private async Task<(BlockHeader Header, byte[] Hash)?> RefreshAsync(bool forceFresh, CancellationToken refreshCt)
        {
            try
            {
                if (_canonicalTip == null)
                    return await RefreshFromPeersAsync(refreshCt).ConfigureAwait(false);
                var tip = await ReadUsableTipAsync(refreshCt).ConfigureAwait(false);
                if (tip == null) return null;
                if (_bundle != null)
                    return await RefreshFromStoreAsync(tip, forceFresh, refreshCt).ConfigureAwait(false);
                return await RefreshFromTipAsync(tip, refreshCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (refreshCt.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "snap.pivot.refresh_failed error_type={ErrorType}", ex.GetType().Name);
                return null;
            }
        }

        private async Task<CanonicalTip> ReadUsableTipAsync(CancellationToken refreshCt)
        {
            var tip = await _canonicalTip.GetLatestAsync(refreshCt).ConfigureAwait(false);
            if (tip == null || tip.BlockNumber <= PivotTrailDistance)
            {
                _logger?.LogDebug("snap.pivot.refresh_noop reason=no_canonical_tip_or_too_low");
                return null;
            }
            return tip;
        }

        private async Task<(BlockHeader Header, byte[] Hash)?> RefreshFromStoreAsync(
            CanonicalTip tip, bool forceFresh, CancellationToken refreshCt)
        {
            var current = (ulong)_rollingPivot.Current.Header.BlockNumber;
            if (!forceFresh && !PivotIsStale(current, tip.BlockNumber, _staleDistanceBlocks))
            {
                _logger?.LogDebug("snap.pivot.refresh_noop current={Current} tip={Tip} stale=false", current, tip.BlockNumber);
                return null;
            }
            var fresh = await TrailedPivot.FreshTrailedPivotAsync(_bundle, tip, 0, _staleDistanceBlocks, refreshCt).ConfigureAwait(false);
            if (fresh.Wait == TrailedPivot.FreshPivotWait.MissingTrailedHeader)
            {
                _logger?.LogWarning("snap.pivot.refresh_waiting_for_trail current={Current} tip={Tip} reason=missing_trailed_header", current, tip.BlockNumber);
                return null;
            }
            if (fresh.Wait == TrailedPivot.FreshPivotWait.LaidHeadersLag)
            {
                _logger?.LogDebug("snap.pivot.refresh_waiting_for_trail current={Current} tip={Tip} trailed_pivot={Trailed} floor={Floor} reason=laid_headers_lag", current, tip.BlockNumber, fresh.TrailedBlock, fresh.Floor);
                return null;
            }
            if (!ShouldRefreshTrailedPivot(forceFresh, current, tip.BlockNumber,
                    fresh.TrailedBlock, (ulong)_rollingPivot.Current.Header.BlockNumber,
                    _staleDistanceBlocks))
                return null;
            return fresh.Pivot;
        }

        private async Task<(BlockHeader Header, byte[] Hash)?> RefreshFromTipAsync(CanonicalTip tip, CancellationToken refreshCt)
        {
            if (TrailedPivot.HasPivotFields(tip))
                return (new BlockHeader { BlockNumber = tip.BlockNumber, StateRoot = tip.StateRoot }, tip.BlockHash);
            return await FetchHeaderAtAsync(tip.BlockNumber - PivotTrailDistance, refreshCt).ConfigureAwait(false);
        }

        private async Task<(BlockHeader Header, byte[] Hash)?> RefreshFromPeersAsync(CancellationToken refreshCt)
        {
            var currentPeerLatest = TrailedPivot.BestPeerLatest(_pool, PivotTrailDistance);
            if (currentPeerLatest == 0) return null;
            return await FetchHeaderAtAsync(currentPeerLatest - PivotTrailDistance, refreshCt).ConfigureAwait(false);
        }

        private async Task<(BlockHeader Header, byte[] Hash)?> FetchHeaderAtAsync(ulong newPivotBlock, CancellationToken refreshCt)
        {
            var newHeaders = await _scheduler.FetchHeadersAsync(newPivotBlock, limit: 1, refreshCt).ConfigureAwait(false);
            if (newHeaders == null || newHeaders.Count == 0) return null;
            var newHeader = newHeaders[0];
            var newHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(newHeader);
            return (newHeader, newHash);
        }
    }
}
