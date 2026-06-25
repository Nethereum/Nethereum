using System;
using Nethereum.DevP2P.Netutil;
using Nethereum.DevP2P.Peering;

namespace Nethereum.DevP2P
{
    public class DevP2PConfig
    {
        public ulong NetworkId { get; set; }
        public byte[] GenesisHash { get; set; }
        public ulong[] ForkBlockNumbers { get; set; } = Array.Empty<ulong>();
        public ulong[] ForkTimestamps { get; set; } = Array.Empty<ulong>();

        public string[] StaticPeers { get; set; } = Array.Empty<string>();

        public int MaxPeers { get; set; } = 25;

        /// <summary>
        /// Maximum concurrent inbound RLPx handshakes accepted from any single
        /// remote IP. Without this, a single attacker IP can exhaust threads /
        /// memory by repeatedly opening sockets faster than individual
        /// handshakes time out, even with <see cref="MaxPeers"/> already
        /// saturated by legitimate peers.
        /// </summary>
        public int MaxInboundPerIP { get; set; } = 9;

        /// <summary>
        /// Hard cap on concurrent inbound connections sharing the same /24
        /// IPv4 subnet (or /48 IPv6 prefix). Per-IP caps alone don't stop an
        /// attacker controlling a /24 (256 IPs) from establishing
        /// MaxInboundPerIP × 256 simultaneous inbound peers without ever
        /// tripping the per-IP gate — Sybil/eclipse pressure from one
        /// allocation. Default 18 (2× MaxInboundPerIP) accepts legitimate
        /// concentration in cloud subnets while bounding the worst case.
        /// Set to 0 to disable the per-subnet check.
        /// </summary>
        public int MaxInboundPerSubnet { get; set; } = 18;

        /// <summary>
        /// Hex-encoded 64-byte enode IDs (no <c>enode://</c> prefix) of peers
        /// that should always be accepted, even when <see cref="MaxPeers"/> is
        /// already saturated. Used by AppChain operators to guarantee admission
        /// for known sequencer and follower peers regardless of inbound
        /// contention.
        /// </summary>
        public string[] TrustedNodeIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// CIDR allow-list restricting which peer IPs are accepted (inbound)
        /// or dialed (outbound). An empty list — the default — means "no
        /// restriction" and preserves unfiltered behaviour. When non-empty,
        /// inbound TCP from an IP outside every entry is closed pre-handshake
        /// (before the per-IP throttle) and outbound dials to unmatched IPs
        /// are skipped. Supports IPv4 and IPv6 CIDR (e.g. <c>10.0.0.0/8</c>,
        /// <c>2001:db8::/32</c>). Primary use case is an AppChain VPC mesh
        /// where the sequencer + followers form a private peer set and no
        /// outside peer should be admitted or contacted even if a poisoned
        /// discovery table advertises one.
        /// </summary>
        public NetRestrict NetRestrict { get; } = new NetRestrict();

        /// <summary>
        /// Outbound dial scheduler knobs: cap on concurrent in-flight dial
        /// attempts, inbound/outbound peer ratio (<c>MaxPeers/2 + 1</c>
        /// outbound cap), and the recent-dial history TTL that suppresses
        /// tight re-dial loops. Trusted peers bypass the concurrent cap and
        /// the ratio but still get history suppression on a shorter TTL.
        /// Defaults: 16 concurrent dials, 5m untrusted history, 30s trusted
        /// history; <see cref="DialSchedulerOptions.MaxPeers"/> should be
        /// kept in sync with <see cref="MaxPeers"/> by the host.
        /// </summary>
        public DialSchedulerOptions DialScheduler { get; set; } = new DialSchedulerOptions();

        public int ConnectTimeoutMs { get; set; } = 10000;
        public int HandshakeTimeoutMs { get; set; } = 10000;
        public int RequestTimeoutMs { get; set; } = 5000;
        public int ReadTimeoutMs { get; set; } = 30000;
        public int PingIntervalMs { get; set; } = 15000;
        public int ReconnectBackoffBaseMs { get; set; } = 1000;
        public int ReconnectBackoffMaxMs { get; set; } = 30000;
        public int MaxConsecutiveFailures { get; set; } = 50;

        public string ClientId { get; set; } = "Nethereum/devp2p";

        public static DevP2PConfig ForDevChain(byte[] genesisHash, ulong networkId = 1337) => new()
        {
            NetworkId = networkId,
            GenesisHash = genesisHash,
            MaxPeers = 5,
            ConnectTimeoutMs = 3000,
            RequestTimeoutMs = 2000,
        };
    }
}
