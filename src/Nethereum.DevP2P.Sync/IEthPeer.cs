using System;
using Nethereum.DevP2P.Rlpx;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Read-only projection of a connected eth/68 or eth/69 peer. Surface
    /// the <see cref="IPeerPool"/> exposes to consumers (request scheduler,
    /// block source, broadcasters). Implemented by <see cref="Eth68PeerSession"/>
    /// for the production registry; test fixtures may stand up alternate
    /// implementations.
    /// </summary>
    public interface IEthPeer
    {
        /// <summary>Locally-generated stable identifier for this connection instance.</summary>
        Guid Id { get; }

        /// <summary>The peer's enode URL as we dialed it (outbound) or as derived
        /// from the inbound connection (server-side). May be empty when the
        /// inbound listener path did not record it.</summary>
        string Enode { get; }

        /// <summary>Remote host (ip:port portion of the enode); may be empty
        /// when only the dialed enode form was recorded.</summary>
        string Host { get; }

        /// <summary>Negotiated eth capability version (68 or 69). 0 if unknown.</summary>
        int EthVersion { get; }

        /// <summary>Peer's reported latest block number (eth/69 LatestBlock or
        /// 0 for eth/68 which only carries total difficulty).</summary>
        ulong PeerLatestBlock { get; }

        /// <summary>Peer's reported EIP-2124 fork hash.</summary>
        uint PeerForkHash { get; }

        /// <summary>Underlying RLPx connection. Subscribers should prefer
        /// <see cref="Disconnected"/> over the inner connection event so the
        /// test boundary can fire synthetic disconnects without a real socket.</summary>
        RlpxConnection Connection { get; }

        /// <summary>Fires once when the peer disconnects (real socket close
        /// in production; explicit trigger in tests). PeerPoolManager
        /// subscribes here to drop + replace the slot.</summary>
        event EventHandler<IEthPeer> Disconnected;
    }
}
