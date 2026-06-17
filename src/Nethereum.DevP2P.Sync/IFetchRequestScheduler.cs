using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Schedules header / body / receipt batch requests across the pool of
    /// connected peers. Picks a peer per request by (lowest in-flight count)
    /// then (highest <see cref="PeerScore"/>) to break ties. On per-request
    /// timeout or transport error, requeues the request to a different peer.
    /// </summary>
    public interface IFetchRequestScheduler
    {
        /// <summary>
        /// Fetch headers starting at <paramref name="startBlock"/>, up to
        /// <paramref name="limit"/> consecutive entries. Returns the
        /// headers the peer sent. Throws on timeout (after exhausting retries
        /// across multiple peers) or when no peer is available.
        /// </summary>
        Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock,
            ulong limit,
            CancellationToken ct);

        /// <summary>
        /// Fetch bodies for the given block hashes. Returns the bodies in the
        /// same positional order the peer responded with — caller must handle
        /// any partial responses (peer may return fewer entries than requested
        /// hashes).
        /// </summary>
        Task<List<BlockBody>> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            CancellationToken ct);
    }

    /// <summary>
    /// Per-peer request worker: dispatches a header / body / receipt batch
    /// to one specific peer. The scheduler picks the peer; this interface is
    /// the abstraction over the per-peer send paths so tests can inject a
    /// controllable stub.
    /// </summary>
    public interface IPeerRequestWorker
    {
        Task<List<BlockHeader>> GetHeadersAsync(
            IEthPeer peer, ulong startBlock, ulong limit, CancellationToken ct);

        Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct);
    }
}
