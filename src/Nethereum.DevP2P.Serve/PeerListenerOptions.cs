using System;
using System.Net;

namespace Nethereum.DevP2P.Serve
{
    /// <summary>
    /// Configuration knobs for <see cref="PeerListener"/>. Deliberately
    /// chain-agnostic — the per-chain values (chain id, genesis hash, fork
    /// schedule) come in through the status template / status builder, not
    /// through here.
    /// </summary>
    public sealed class PeerListenerOptions
    {
        /// <summary>TCP port to bind for incoming RLPx connections.
        /// 0 = OS-assigned (read the actual port after Start).</summary>
        public int ListenPort { get; set; } = 30303;

        /// <summary>Local bind address. Null = <see cref="IPAddress.Any"/>.</summary>
        public IPAddress BindAddress { get; set; }

        /// <summary>Hard cap on concurrent inbound peers. New connections beyond
        /// this get TooManyPeers disconnect.</summary>
        public int MaxInboundPeers { get; set; } = 50;

        /// <summary>Per-IP concurrent inbound cap. Sockets past the cap are
        /// closed before the handshake reads a single byte.</summary>
        public int MaxInboundPerIP { get; set; } = 3;

        /// <summary>Per-message size ceiling. Anything larger is rejected as a
        /// protocol violation.</summary>
        public int MaxFrameSize { get; set; } = 16 * 1024 * 1024;

        /// <summary>Handshake auth/ack deadline in milliseconds. After this
        /// the socket is torn down — bounds work an unresponsive client
        /// can cost us.</summary>
        public int HandshakeTimeoutMs { get; set; } = 10_000;

        /// <summary>Idle disconnect window. Peer that hasn't sent a useful
        /// message in this long is dropped (independent of p2p layer pings,
        /// which keep the TCP socket alive but don't count as application
        /// activity).</summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>Enable EIP-4444-style snap/1 serving (default true). Turn
        /// off for pure relay nodes that don't have state available.</summary>
        public bool ServeSnap { get; set; } = true;

        /// <summary>Try to open the listen port via UPnP / NAT-PMP on start so
        /// peers behind NAT can be dialled inbound. Currently a no-op stub.</summary>
        public bool EnableUPnP { get; set; } = false;

        /// <summary>Trusted node ids (64-byte secp256k1 pubkeys, hex with no
        /// "enode://" prefix). Trusted peers bypass <see cref="MaxInboundPeers"/>
        /// after the handshake reveals their node id.</summary>
        public string[] TrustedNodeIds { get; set; } = Array.Empty<string>();

        /// <summary>Client identifier reported in the p2p Hello. Recommended
        /// format: <c>"AppName/version/os-arch/dotnet-X.Y"</c>.</summary>
        public string ClientId { get; set; } = "Nethereum.DevP2P.Serve/0.1";

        /// <summary>
        /// When true, the listener echoes the remote peer's Status fields
        /// (chain id, genesis hash, fork hash, head) instead of asserting our
        /// own. Useful as a generic relay that can serve any chain a peer
        /// dials. When false the caller MUST provide a status template via
        /// <see cref="PeerListener"/>'s constructor — otherwise the handshake
        /// has no chain identity to send.
        /// </summary>
        public bool MirrorRemoteStatus { get; set; } = true;
    }
}
