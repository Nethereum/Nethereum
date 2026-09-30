using System.Numerics;

namespace Nethereum.AccountAbstraction.Configuration
{
    public interface IAADeploymentAddressProvider
    {
        bool TryGet(BigInteger chainId, out AADeploymentAddresses addresses);

        AADeploymentAddresses Get(BigInteger chainId);

        void Set(BigInteger chainId, AADeploymentAddresses addresses);
    }
}
