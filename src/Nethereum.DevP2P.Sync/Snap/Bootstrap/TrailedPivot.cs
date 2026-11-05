using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapSyncOrchestrator;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class TrailedPivot
    {
        internal enum FreshPivotWait { None, MissingTrailedHeader, LaidHeadersLag }

        internal readonly record struct FreshPivot(
            (BlockHeader Header, byte[] Hash)? Pivot, FreshPivotWait Wait, ulong TrailedBlock, ulong Floor);

        internal static async Task<FreshPivot> FreshTrailedPivotAsync(
            IChainStoreBundle bundle, CanonicalTip tip, ulong savedPivotFloor, ulong staleDistanceBlocks, CancellationToken ct)
        {
            ulong maxLag = Math.Max(Math.Min(PivotTrailDistance, staleDistanceBlocks), PivotServeTrailDistance);
            ulong freshFloor = tip.BlockNumber > maxLag ? tip.BlockNumber - maxLag : 0;
            ulong floor = Math.Max(savedPivotFloor, freshFloor);
            var trailed = await FromStoreAsync(bundle, tip, ct).ConfigureAwait(false);
            if (!trailed.HasValue) return new FreshPivot(null, FreshPivotWait.MissingTrailedHeader, 0, floor);
            ulong trailedBlock = (ulong)trailed.Value.Header.BlockNumber;
            if (trailedBlock < floor) return new FreshPivot(null, FreshPivotWait.LaidHeadersLag, trailedBlock, floor);
            return new FreshPivot(trailed, FreshPivotWait.None, trailedBlock, floor);
        }

        internal static async Task<ulong> WaitForSnapPeerLatestAsync(IPeerPool pool, ulong pivotDistance, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var bestPeerLatest = BestPeerLatest(pool, pivotDistance);
                if (bestPeerLatest > 0) return bestPeerLatest;
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }

        internal static ulong BestPeerLatest(IPeerPool pool, ulong minExclusive)
            => pool.ActivePeers
                .OfType<SyncPeerSession>()
                .Where(s => s.PeerLatestBlock > minExclusive)
                .Select(s => s.PeerLatestBlock)
                .DefaultIfEmpty(0UL)
                .Max();

        internal static bool HasPivotFields(CanonicalTip tip)
            => tip.StateRoot != null && tip.StateRoot.Length == 32
                && tip.BlockHash != null && tip.BlockHash.Length == 32;

        internal static async Task<(BlockHeader Header, byte[] Hash)?> FromStoreAsync(
            IChainStoreBundle bundle, CanonicalTip tip, CancellationToken ct)
        {
            if (tip == null || tip.BlockHash == null || tip.BlockHash.Length != 32) return null;
            if (tip.BlockNumber < 1) return null;

            ulong laidTip = HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState());
            ulong effectiveTip = Math.Min(tip.BlockNumber, laidTip);
            if (effectiveTip == 0 || effectiveTip < PivotServeTrailDistance + 1) return null;

            ulong pivotNumber = effectiveTip - PivotServeTrailDistance;

            byte[] expectedHash = effectiveTip == tip.BlockNumber ? tip.BlockHash : null;
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
    }
}
