using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.DevP2P.NodeDb;
using Nethereum.Signer;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeConfig
    {
        public ChainNodeStorageConfig Storage { get; set; } = new ChainNodeStorageConfig();

        public ChainNodeNetworkConfig Network { get; set; } = new ChainNodeNetworkConfig();

        public ChainNodeSyncConfig Sync { get; set; } = new ChainNodeSyncConfig();

        public ChainNodeRpcConfig Rpc { get; set; } = new ChainNodeRpcConfig();

        public ChainNodeMaintenanceConfig Maintenance { get; set; } = new ChainNodeMaintenanceConfig();

        public ChainNodeMempoolConfig Mempool { get; set; } = new ChainNodeMempoolConfig();

        public EthECKey ResolveNodeKey(Action<string>? log = null) =>
            Network.ResolveNodeKey(Storage.DataDirectory, log);

        public PersistentPeerCache OpenPeerCache(Action<string>? log = null) =>
            Network.OpenPeerCache(Storage.DataDirectory, log);

        public IReadOnlyList<string> ResolveDialEnodes() =>
            FollowPeerThenTrustedPeers()
                .Where(enode => !string.IsNullOrWhiteSpace(enode))
                .Distinct()
                .ToList();

        public string[] ResolveTrustedNodeIds() => ResolveTrustedNodeIds(out _);

        public string[] ResolveTrustedNodeIds(out string[] malformedEnodes) =>
            ChainNodeNetworkConfig.NodeIdsOf(ResolveDialEnodes(), out malformedEnodes)
                .Concat(Network.TrustedNodeIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToArray();

        public bool FollowsAPeer =>
            Sync.Mode != SyncMode.None && !string.IsNullOrEmpty(Sync.FollowPeerEnode);

        private IEnumerable<string> FollowPeerThenTrustedPeers()
        {
            var trusted = Network.TrustedPeers ?? Array.Empty<string>();

            return string.IsNullOrEmpty(Sync.FollowPeerEnode)
                ? trusted
                : new[] { Sync.FollowPeerEnode! }.Concat(trusted);
        }
    }
}
