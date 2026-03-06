using System.Collections.Generic;

namespace Nethereum.AppChain.P2P.DotNetty
{
    public class DotNettyConfig
    {
        public string ListenAddress { get; set; } = "0.0.0.0";
        public int ListenPort { get; set; } = 30303;
        public long ChainId { get; set; } = 1;
        public string? NodePrivateKey { get; set; }
        public List<string> BootstrapNodes { get; set; } = new();
        public HashSet<string> AllowedPeers { get; set; } = new();
        public int MaxConnections { get; set; } = 50;
        public int TargetConnections { get; set; } = 25;
        public int ConnectionTimeoutMs { get; set; } = 10000;
        public int HandshakeTimeoutSeconds { get; set; } = 30;
        public int IdleTimeoutSeconds { get; set; } = 60;
        public int PeerDiscoveryIntervalMs { get; set; } = 30000;
        public bool RequireAuthentication { get; set; } = false;
        public bool UseTls { get; set; } = false;
        public string? TlsCertificatePath { get; set; }
        public string? TlsCertificatePassword { get; set; }
        public string? TlsTargetHost { get; set; }
        public int MaxMessageSize { get; set; } = 10 * 1024 * 1024;
        public int WorkerThreads { get; set; } = 0;
        public int MessageQueueCapacity { get; set; } = 10000;
        public int MaxConnectionsPerIp { get; set; } = 5;

        public static DotNettyConfig Default => new()
        {
            ListenAddress = "0.0.0.0",
            ListenPort = 30303,
            ChainId = 1,
            MaxConnections = 50,
            TargetConnections = 25,
            ConnectionTimeoutMs = 10000,
            HandshakeTimeoutSeconds = 30,
            IdleTimeoutSeconds = 60,
            PeerDiscoveryIntervalMs = 30000,
            RequireAuthentication = false,
            UseTls = false,
            MaxMessageSize = 10 * 1024 * 1024
        };

        public static DotNettyConfig ForDevelopment(int port = 30303, long chainId = 31337) => new()
        {
            ListenAddress = "127.0.0.1",
            ListenPort = port,
            ChainId = chainId,
            MaxConnections = 10,
            TargetConnections = 5,
            ConnectionTimeoutMs = 5000,
            HandshakeTimeoutSeconds = 15,
            IdleTimeoutSeconds = 120,
            PeerDiscoveryIntervalMs = 10000,
            RequireAuthentication = false,
            UseTls = false,
            MaxMessageSize = 10 * 1024 * 1024
        };

        public static DotNettyConfig ForPrivateNetwork(long chainId, string nodePrivateKey, IEnumerable<string>? allowedPeers = null) => new()
        {
            ListenAddress = "0.0.0.0",
            ListenPort = 30303,
            ChainId = chainId,
            NodePrivateKey = nodePrivateKey,
            AllowedPeers = allowedPeers != null ? new HashSet<string>(allowedPeers) : new HashSet<string>(),
            MaxConnections = 25,
            TargetConnections = 15,
            ConnectionTimeoutMs = 10000,
            HandshakeTimeoutSeconds = 30,
            IdleTimeoutSeconds = 60,
            PeerDiscoveryIntervalMs = 30000,
            RequireAuthentication = true,
            UseTls = false,
            MaxMessageSize = 10 * 1024 * 1024
        };

        public static DotNettyConfig ForProduction(long chainId, string nodePrivateKey, string tlsCertPath, string tlsCertPassword) => new()
        {
            ListenAddress = "0.0.0.0",
            ListenPort = 30303,
            ChainId = chainId,
            NodePrivateKey = nodePrivateKey,
            MaxConnections = 50,
            TargetConnections = 25,
            ConnectionTimeoutMs = 10000,
            HandshakeTimeoutSeconds = 30,
            IdleTimeoutSeconds = 60,
            PeerDiscoveryIntervalMs = 30000,
            RequireAuthentication = true,
            UseTls = true,
            TlsCertificatePath = tlsCertPath,
            TlsCertificatePassword = tlsCertPassword,
            MaxMessageSize = 10 * 1024 * 1024
        };

        public DotNettyConfig WithBootstrapNodes(params string[] nodes)
        {
            BootstrapNodes.AddRange(nodes);
            return this;
        }

        public DotNettyConfig WithAllowedPeers(params string[] peers)
        {
            foreach (var peer in peers)
                AllowedPeers.Add(peer.ToLowerInvariant());
            return this;
        }
    }
}
