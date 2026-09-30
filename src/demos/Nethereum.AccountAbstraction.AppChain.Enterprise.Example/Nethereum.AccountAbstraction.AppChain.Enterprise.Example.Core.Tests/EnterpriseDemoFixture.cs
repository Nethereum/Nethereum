using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class EnterpriseDemoCollection : ICollectionFixture<EnterpriseDemoFixture>
    {
        public const string COLLECTION_NAME = EnterpriseDemoFixture.COLLECTION_NAME;
    }

    public class EnterpriseDemoFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "AaAppChainEnterpriseDemo";

        public EnterpriseDemoHostBootstrap Bootstrap { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Bootstrap = await EnterpriseDemoHostBootstrap.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Bootstrap.Bootstrap.DisposeAsync();
        }

        public SessionState NewReadySession()
        {
            var session = new SessionState();
            Bootstrap.PublishInto(session);
            return session;
        }

        public Task FundAsync(string address, decimal ether = 10m) =>
            Bootstrap.Bootstrap.Node.SetBalanceAsync(address, Web3.Web3.Convert.ToWei(ether));
    }
}
