using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// A pool of live eth-handshaken peers maintained at a target size. The
    /// pool owns the outbound dial loop, per-peer lifetime, and the ban list.
    /// Stage 1 surface; Stage 2 layers peer scoring on top of the same
    /// interface (eviction policy becomes score-based rather than insertion-
    /// ordered).
    /// </summary>
    public interface IPeerPool : IAsyncDisposable
    {
        /// <summary>Snapshot of currently connected peers. Safe to enumerate
        /// concurrently with pool mutations.</summary>
        IReadOnlyCollection<IEthPeer> ActivePeers { get; }

        /// <summary>Live membership check for a peer by its pool-assigned id.
        /// Used by schedulers that iterate an <see cref="ActivePeers"/>
        /// snapshot and want to re-confirm liveness before reserving work
        /// against a peer that may have disconnected since the snapshot.
        /// Default implementation falls back to scanning
        /// <see cref="ActivePeers"/>; production pools should override with an
        /// O(1) lookup.</summary>
        bool IsPeerActive(Guid peerId)
        {
            foreach (var peer in ActivePeers)
            {
                if (peer.Id == peerId) return true;
            }
            return false;
        }

        /// <summary>The target steady-state pool size. The dialer fires until
        /// <see cref="ActivePeers"/>.Count reaches this number, then idles
        /// until a slot opens via disconnect or eviction.</summary>
        int TargetPeerCount { get; }

        /// <summary>Fires once per peer after it joins the pool. Synchronous
        /// handlers must be cheap; do not block.</summary>
        event EventHandler<IEthPeer> PeerAdded;

        /// <summary>Fires once per peer after it leaves the pool (disconnect
        /// or eviction).</summary>
        event EventHandler<IEthPeer> PeerRemoved;

        /// <summary>Start the background dialer + reseed loops. Idempotent for
        /// duplicate calls. Returns once both loops are running; the pool
        /// reaches target asynchronously.</summary>
        Task StartAsync(CancellationToken ct);

        /// <summary>Mark a peer as banned for the current run and drop the
        /// connection if it's currently in the pool. Subsequent reseed cycles
        /// must skip this enode until <see cref="ClearAllBansAsync"/> is
        /// called.</summary>
        Task BanAndDropAsync(string enode, string reason, CancellationToken ct);

        /// <summary>Clear the per-run ban list. Used after a successful rewind
        /// to allow re-evaluation of peers that were previously diverging.</summary>
        Task ClearAllBansAsync();
    }
}
