using Nethereum.XUnitEthereumClients;
using Xunit;
 // ReSharper disable ConsiderUsingConfigureAwait
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.AccountAbstraction.IntegrationTests
{
    public static class SharedEthereumFixture
    {
        private static EthereumClientIntegrationFixture? _instance;
        private static readonly object _lock = new object();
        private static int _referenceCount = 0;

        public static EthereumClientIntegrationFixture GetOrCreate()
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new EthereumClientIntegrationFixture();
                }
                _referenceCount++;
                return _instance;
            }
        }

        public static void Release()
        {
            lock (_lock)
            {
                _referenceCount--;
                if (_referenceCount <= 0 && _instance != null)
                {
                    _instance.Dispose();
                    _instance = null;
                    _referenceCount = 0;
                }
            }
        }
    }

    [CollectionDefinition(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthereumClientFixtureCollection : ICollectionFixture<EthereumClientIntegrationFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}