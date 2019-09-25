using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class PersonalLockAccountTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public PersonalLockAccountTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) :
            base(ethereumClientIntegrationFixture, TestSettingsCategory.localTestNet)
        {
        }

        [Fact]
        public async void ShouldLockAccountAndReturnTrue()
        {
            if(Settings.IsParity()) return;

            var result = await ExecuteAsync();
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var personalLockAccount = new PersonalLockAccount(client);
            return await personalLockAccount.SendRequestAsync(Settings.GetDefaultAccount());
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalLockAccount);
        }
    }
}
        