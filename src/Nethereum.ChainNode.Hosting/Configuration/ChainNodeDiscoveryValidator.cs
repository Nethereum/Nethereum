using System;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeDiscoveryValidator
    {
        public static bool IsRequested(ChainNodeDiscoveryConfig discovery) =>
            !discovery.DisableDiscv4 || !discovery.DisableDiscv5
            || discovery.Discv4Port != 0 || discovery.Discv5Port != 0;

        public static void RefuseIfRequested(ChainNodeNetworkConfig network, string nodeTypeName)
        {
            if (!IsRequested(network.Discovery)) return;

            throw new InvalidOperationException(
                $"discv4/discv5 peer discovery is not supported on this node type ({nodeTypeName}); "
                + $"{nodeTypeName} uses a curated peer mesh (Network:TrustedPeers/TrustedBootnodes) instead. "
                + "Remove Network:Discovery:DisableDiscv4/DisableDiscv5/Discv4Port/Discv5Port from your "
                + $"config — they must stay at their {nodeTypeName} defaults (DisableDiscv4=true, "
                + "DisableDiscv5=true, Discv4Port=0, Discv5Port=0).");
        }
    }
}
