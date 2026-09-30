using System;
using Nethereum.ChainNode.Hosting.Configuration;

namespace Nethereum.DevChain.Configuration
{
    public static class DevChainServerConfigValidator
    {
        public static void Validate(DevChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            ChainNodeDiscoveryValidator.RefuseIfRequested(config.Node.Network, "DevChain");
        }
    }
}
