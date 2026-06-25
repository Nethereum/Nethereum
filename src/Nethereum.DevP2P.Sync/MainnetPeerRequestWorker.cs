using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Production <see cref="IPeerRequestWorker"/>. Dispatches per-request to
    /// the matching <see cref="MainnetPeerSession"/> via <see cref="IEthPeer"/>
    /// concrete-type narrowing. The <see cref="FetchRequestScheduler"/> never
    /// sees the underlying session type — peer selection works on
    /// <see cref="IEthPeer"/> alone, with the optional peer-filter the
    /// scheduler applies (e.g. <c>SupportsSnap</c>) gating snap-method peers
    /// upstream so this worker only ever sees a session that can serve the
    /// requested op.
    /// </summary>
    public sealed class MainnetPeerRequestWorker : IPeerRequestWorker
    {
        public Task<List<BlockHeader>> GetHeadersAsync(
            IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetHeadersAsync(startBlock, limit, skip: 0, reverse: reverse, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<BlockHeader>> GetHeadersByHashAsync(
            IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetHeadersByHashAsync(startHash, limit, skip: 0, reverse: false, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetBodiesAsync(new List<byte[]>(blockHashes), ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetReceiptsAsync(new List<byte[]>(blockHashes), ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(
            IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetByteCodesAsync(codeHashes, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetTrieNodesAsync(stateRoot, paths, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"MainnetPeerRequestWorker requires MainnetPeerSession-backed peers; got {peer.GetType().Name}.");
        }
    }
}
