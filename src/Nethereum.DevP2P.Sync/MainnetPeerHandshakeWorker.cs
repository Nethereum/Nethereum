using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Production <see cref="IPeerHandshakeWorker"/> implementation. Wraps
    /// <see cref="MainnetPeerSession"/>.ConnectAsync, which implements
    /// <see cref="IEthPeer"/> directly — the returned session IS the peer
    /// projection. Stage 3's request scheduler keeps the underlying session
    /// for header / body / receipt requests (cast back from IEthPeer).
    /// </summary>
    public sealed class MainnetPeerHandshakeWorker : IPeerHandshakeWorker
    {
        public async Task<IEthPeer> HandshakeAsync(
            string enode,
            TimeSpan timeout,
            ulong minPeerLatestBlock,
            CancellationToken ct)
        {
            return await MainnetPeerSession.ConnectAsync(enode, timeout, ct, minPeerLatestBlock)
                .ConfigureAwait(false);
        }
    }
}
