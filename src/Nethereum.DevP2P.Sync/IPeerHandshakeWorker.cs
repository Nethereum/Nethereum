using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Per-peer dial + RLPx handshake + eth/68 or eth/69 status exchange.
    /// Production implementation wraps <see cref="MainnetPeerSession"/>.ConnectAsync;
    /// test fixtures supply <c>LoopbackPeerEndpoint</c>-backed implementations
    /// for deterministic peer-pool scenarios.
    /// </summary>
    public interface IPeerHandshakeWorker
    {
        /// <summary>
        /// Dial the enode, complete the RLPx handshake, complete the eth/68 or
        /// eth/69 Status exchange, and return the resulting peer projection.
        /// </summary>
        /// <param name="enode">Enode URL to dial.</param>
        /// <param name="timeout">Per-stage timeout (connect / handshake / status).</param>
        /// <param name="minPeerLatestBlock">Useless-peer floor — peers reporting
        /// less than this are rejected as <see cref="MainnetPeerSession.UselessPeerException"/>
        /// (the pool then bans them for the run).</param>
        /// <param name="ct">Cancellation.</param>
        /// <returns>The handshaken peer projection. Throws on dial / handshake /
        /// status failure; throws <see cref="MainnetPeerSession.UselessPeerException"/>
        /// on useless-peer rejection so the dialer can distinguish "ban this one
        /// for the run" from "transient transport error".</returns>
        Task<IEthPeer> HandshakeAsync(
            string enode,
            TimeSpan timeout,
            ulong minPeerLatestBlock,
            CancellationToken ct);
    }
}
