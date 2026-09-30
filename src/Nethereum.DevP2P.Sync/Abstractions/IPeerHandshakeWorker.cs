using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IPeerHandshakeWorker
    {
        Task<IEthPeer> HandshakeAsync(
            string enode,
            TimeSpan timeout,
            ulong minPeerLatestBlock,
            CancellationToken ct);
    }
}
