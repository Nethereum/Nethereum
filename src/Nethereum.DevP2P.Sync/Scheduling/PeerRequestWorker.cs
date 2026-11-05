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
            return Session(peer).GetHeadersAsync(startBlock, limit, skip: 0, reverse: reverse, ct);
        }

        public Task<List<BlockHeader>> GetHeadersByHashAsync(
            IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
        {
            return Session(peer).GetHeadersByHashAsync(startHash, limit, skip: 0, reverse: false, ct);
        }

        public Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            return Session(peer).GetBodiesAsync(new List<byte[]>(blockHashes), ct);
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            return Session(peer).GetReceiptsAsync(new List<byte[]>(blockHashes), ct);
        }

        public Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, IReadOnlyList<ulong> gasUsedByBlock, CancellationToken ct)
        {
            return Session(peer).GetReceiptsAsync(new List<byte[]>(blockHashes), gasUsedByBlock, ct);
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
        {
            return Session(peer).GetAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, ct);
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            return Session(peer).GetStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, ct);
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(
            IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
        {
            return Session(peer).GetByteCodesAsync(codeHashes, responseBytes, ct);
        }

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct)
        {
            return Session(peer).GetTrieNodesAsync(stateRoot, paths, responseBytes, ct);
        }

        private static SyncPeerSession Session(IEthPeer peer)
            => peer as SyncPeerSession
               ?? throw new System.InvalidOperationException(
                   $"PeerRequestWorker requires SyncPeerSession-backed peers; got {peer.GetType().Name}.");
    }
}
