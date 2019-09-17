using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthAccountsTester : RPCRequestTester<String[]>, IRPCRequestTester
    {
        public EthAccountsTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) : 
            base(ethereumClientIntegrationFixture, TestSettings.GethLocalSettings)
        {
        }

        [Fact]
        public async void ShouldRetrieveAccounts()
        {
            var accounts = await ExecuteAsync();
            Assert.True(accounts.Length > 0);
        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var ethAccounts = new EthAccounts(client);
            return await ethAccounts.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof (EthAccounts);
        }
    }
}