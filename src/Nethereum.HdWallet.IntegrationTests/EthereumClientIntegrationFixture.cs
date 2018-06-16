using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.HdWallet.IntegrationTests
{
    [CollectionDefinition(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthereumClientFixtureCollection : ICollectionFixture<EthereumClientIntegrationFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

}
