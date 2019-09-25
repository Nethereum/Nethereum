using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class PersonalListAccountsTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public PersonalListAccountsTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) :
            base(ethereumClientIntegrationFixture, TestSettingsCategory.localTestNet)
        {
        }

        [Fact]
        public async void ShouldRetrieveTheAccounts()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var personalListAccounts = new PersonalListAccounts(client);
            
            var accounts = await personalListAccounts.SendRequestAsync();
            return accounts;
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalSignAndSendTransaction);
        }
    }
}
        