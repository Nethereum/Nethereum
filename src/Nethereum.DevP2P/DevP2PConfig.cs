using System;
using Nethereum.DevP2P.Netutil;
using Nethereum.DevP2P.Peering;
using Nethereum.Documentation;

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

        public int MaxInboundPerIP { get; set; } = 9;

        public int MaxInboundPerSubnet { get; set; } = 18;

        public string[] TrustedNodeIds { get; set; } = Array.Empty<string>();

        public NetRestrict NetRestrict { get; } = new NetRestrict();

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

        /// <summary>
        /// Advertise snap/2 (EIP-8189, "BAL-Based State Healing") in the local
        /// Hello, alongside snap/1. Default FALSE.
        /// <para>
        /// snap/2 REMOVES GetTrieNodes/TrieNodes (0x06/0x07) — "Replaced by
        /// BAL-based healing" — while the snap/1 heal phase depends on
        /// GetTrieNodes. Negotiating snap/2 with a peer that also advertises
        /// it drops trie-node healing for that peer. Mirrors geth's own
        /// feature gate (<c>MakeProtocols(&#8230;, snapV2 bool)</c>).
        /// </para>
        /// </summary>
        public bool AdvertiseSnap2 { get; set; } = false;

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "DevP2PConfig.ForDevChain — dev-chain config factory")]
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
