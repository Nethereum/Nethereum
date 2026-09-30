using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Nethereum.DevP2P;
using Nethereum.DevP2P.NodeDb;
using Nethereum.Signer;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeNetworkConfig
    {
        public bool Serve { get; set; } = true;

        public int ListenPort { get; set; } = 30303;

        public IPAddress BindAddress { get; set; } = IPAddress.Any;

        public int DialBudgetPerSecond { get; set; } = 5;

        public int MaxPeersPerIPv4Subnet { get; set; } = 10;

        public int MaxPeersPerIPv6Subnet { get; set; } = 10;

        public string? NodeKeyFile { get; set; }

        public string? NodeKeyHex { get; set; }

        public string[] TrustedPeers { get; set; } = Array.Empty<string>();

        public string[] TrustedBootnodes { get; set; } = Array.Empty<string>();

        public string[] TrustedNodeIds { get; set; } = Array.Empty<string>();

        public int TargetPeerCount { get; set; } = 16;

        public int MaxConcurrentDials { get; set; } = 10;

        public int MaxInboundPeers { get; set; } = 25;

        public int MaxInboundPerIP { get; set; } = 9;

        public int HandshakeTimeoutMs { get; set; } = 10_000;

        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        public bool MirrorRemoteStatus { get; set; }

        public string ClientId { get; set; } = "Nethereum";

        public ChainNodeDiscoveryConfig Discovery { get; set; } = new ChainNodeDiscoveryConfig();

        public EthECKey ResolveNodeKey(string dataDirectory, Action<string>? log = null)
        {
            if (!string.IsNullOrEmpty(NodeKeyHex)) return new EthECKey(NodeKeyHex);

            var path = string.IsNullOrWhiteSpace(NodeKeyFile)
                ? Path.Combine(dataDirectory ?? ".", "nodekey")
                : NodeKeyFile!;

            return NodeKeyStore.LoadOrCreate(path, log ?? (_ => { }));
        }

        public PersistentPeerCache OpenPeerCache(string dataDirectory, Action<string>? log = null) =>
            new PersistentPeerCache(Path.Combine(dataDirectory ?? ".", "peer-cache.json"), log ?? (_ => { }));

        public static string[] NodeIdsOf(IEnumerable<string> enodes) => NodeIdsOf(enodes, out _);

        public static string[] NodeIdsOf(IEnumerable<string> enodes, out string[] malformedEnodes)
        {
            var ids = new List<string>();
            var malformed = new List<string>();

            if (enodes != null)
            {
                foreach (var enode in enodes)
                {
                    if (EnodeUrl.TryParse(enode, out var parsed))
                        ids.Add(parsed.PeerId);
                    else
                        malformed.Add(enode);
                }
            }

            malformedEnodes = malformed.ToArray();
            return ids.ToArray();
        }
    }
}
