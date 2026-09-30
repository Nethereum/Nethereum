using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace Nethereum.AccountAbstraction.Configuration
{
    public class AADeploymentAddressProvider : IAADeploymentAddressProvider
    {
        private readonly ConcurrentDictionary<BigInteger, AADeploymentAddresses> _addresses = new();

        public bool TryGet(BigInteger chainId, out AADeploymentAddresses addresses)
        {
            return _addresses.TryGetValue(chainId, out addresses);
        }

        public AADeploymentAddresses Get(BigInteger chainId)
        {
            if (!_addresses.TryGetValue(chainId, out var addresses))
                throw new InvalidOperationException(
                    $"No AADeploymentAddresses registered for chain id {chainId}. Call Set(chainId, addresses) " +
                    "during startup (or after deploying the AA stack on a dev chain) before resolving it.");

            return addresses;
        }

        public void Set(BigInteger chainId, AADeploymentAddresses addresses)
        {
            _addresses[chainId] = addresses;
        }
    }
}
