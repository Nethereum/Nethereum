using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

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
        /// <param name="reverse">When true, fetches headers descending from <paramref name="startBlock"/>
        /// down to <paramref name="startBlock"/> - <paramref name="limit"/> + 1. Used by the
        /// trusted-tip backward walker; see <c>BackwardBlockWalker</c>. Default false preserves the
        /// forward-fetch semantics every existing caller relies on.</param>
        Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock,
            ulong limit,
            CancellationToken ct,
            bool reverse = false);

        /// <summary>
        /// Fetch headers anchored at a block HASH rather than a number, retrying
        /// across peers on transport error/timeout. A peer still syncing serves
        /// canonical-only by number (empty for a recent block) but serves any block
        /// it holds BY HASH, so this resolves a trusted-hash pivot header that a
        /// by-number fetch cannot. The caller must verify the returned header hashes
        /// to the requested hash.
        /// </summary>
        Task<List<BlockHeader>> FetchHeadersByHashAsync(
            byte[] startHash,
            ulong limit,
            CancellationToken ct)
            => throw new NotSupportedException(
                "FetchHeadersByHashAsync is not implemented by this scheduler.");

        /// <summary>
        /// Fetch bodies for the given block hashes. Returns the bodies in the
        /// same positional order the peer responded with — caller must handle
        /// any partial responses (peer may return fewer entries than requested
        /// hashes).
        /// </summary>
        Task<List<BlockBody>> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            CancellationToken ct);

        /// <summary>
        /// Same as <see cref="FetchBodiesAsync(IReadOnlyList{byte[]}, CancellationToken)"/>
        /// but the caller can pin a set of peer IDs to skip (a peer that just returned
        /// bodies failing header-tx_root validation, etc.) AND learns the IDs of the
        /// peers that did serve the response. Together these let the caller perform
        /// peer rotation across retry attempts after content-level validation failures
        /// — transport-level errors are already rotated inside the scheduler, but
        /// content failures are only detectable upstream.
        /// </summary>
        Task<BodyFetchResult> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            IReadOnlyCollection<Guid>? excludePeers,
            CancellationToken ct);

        /// <summary>
        /// Fetch receipts for the given block hashes. Returns receipts in the
        /// same positional order the peer responded with; partial responses
        /// must be handled by the caller (peer may return fewer entries than
        /// requested). Used by Phase 1 historical block backfill so the node
        /// can serve <c>eth_getTransactionReceipt</c> for any block under the
        /// pivot.
        /// </summary>
        Task<List<List<Receipt>>> FetchReceiptsAsync(
            IReadOnlyList<byte[]> blockHashes,
            CancellationToken ct);

        /// <summary>
        /// Fetch a snap/1 account-range chunk. Routes to a snap-capable peer
        /// (filters by <c>MainnetPeerSession.SupportsSnap</c>), retries on
        /// timeout / transport error across other snap peers. Throws if no
        /// snap-capable peer is available after the per-request deadline.
        /// </summary>
        Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        /// <summary>Fetch a snap/1 storage-range chunk; same scheduling semantics as account range.</summary>
        Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        /// <summary>Fetch contract bytecodes by keccak hash; same scheduling semantics.</summary>
        Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct);

        /// <summary>Fetch raw trie nodes by path under a state root; same scheduling semantics.</summary>
        Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct);

        /// <summary>
        /// True when <paramref name="peer"/> is currently eligible to serve snap STATE requests —
        /// it advertises snap/1 and is not in the temporary state-serving quarantine applied to peers
        /// that answered a state request with no data. Used for observability (serving vs quarantined
        /// counts). Default returns true so non-snap schedulers don't skew the metric.
        /// </summary>
        bool IsSnapStateServing(IEthPeer peer) => true;
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
            IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct);

        Task<List<BlockHeader>> GetHeadersByHashAsync(
            IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
            => throw new NotSupportedException(
                "GetHeadersByHashAsync is not implemented by this worker.");

        Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct);

        Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct);

        Task<AccountRangeMessage> GetAccountRangeAsync(
            IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        Task<StorageRangesMessage> GetStorageRangesAsync(
            IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct);

        Task<ByteCodesMessage> GetByteCodesAsync(
            IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct);

        Task<TrieNodesMessage> GetTrieNodesAsync(
            IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct);
    }
}
