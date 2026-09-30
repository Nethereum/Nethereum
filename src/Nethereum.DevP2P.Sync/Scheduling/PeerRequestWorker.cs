using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    public sealed class PeerRequestWorker : IPeerRequestWorker
    {
        public Task<List<BlockHeader>> GetHeadersAsync(
            IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetHeadersAsync(startBlock, limit, skip: 0, reverse: reverse, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<BlockHeader>> GetHeadersByHashAsync(
            IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetHeadersByHashAsync(startHash, limit, skip: 0, reverse: false, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetBodiesAsync(new List<byte[]>(blockHashes), ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetReceiptsAsync(new List<byte[]>(blockHashes), ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(
            IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetByteCodesAsync(codeHashes, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct)
        {
            if (peer is SyncPeerSession session)
                return session.GetTrieNodesAsync(stateRoot, paths, responseBytes, ct);
            throw new System.InvalidOperationException(
                $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
        }
    }
}
