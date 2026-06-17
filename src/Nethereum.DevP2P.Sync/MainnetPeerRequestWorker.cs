using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Production <see cref="IPeerRequestWorker"/>. Dispatches per-request to
    /// the matching <see cref="MainnetPeerSession"/> via <see cref="IEthPeer"/>
    /// concrete-type narrowing. The <see cref="FetchRequestScheduler"/> never
    /// sees the underlying session type — peer selection works on
    /// <see cref="IEthPeer"/> alone.
    /// </summary>
    public sealed class MainnetPeerRequestWorker : IPeerRequestWorker
    {
        public Task<List<BlockHeader>> GetHeadersAsync(
            IEthPeer peer, ulong startBlock, ulong limit, CancellationToken ct)
        {
            if (peer is MainnetPeerSession session)
                return session.GetHeadersAsync(startBlock, limit, ct);
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
    }
}
