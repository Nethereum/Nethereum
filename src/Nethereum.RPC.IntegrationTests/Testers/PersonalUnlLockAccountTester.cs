using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class PersonalUnlLockAccountTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public PersonalUnlLockAccountTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) :
            base(ethereumClientIntegrationFixture, TestSettingsCategory.localTestNet)
        {
        }

        [Fact]
        public async void ShouldUnLockAccountAndReturnTrue()
        {
            var result = await ExecuteAsync();
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var personalunlockAccount = new PersonalUnlockAccount(client);
            ulong? duration = null;

            bool unlocked = await personalunlockAccount.SendRequestAsync(
                Settings.GetDefaultAccount(), Settings.GetDefaultAccountPassword(), duration);

            if(!unlocked) 
                return false;

            if(Settings.IsParity()) 
                return true;

            return await
                personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(),
                    Settings.GetDefaultAccountPassword(), 30);
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalUnlockAccount);
        }
    }
}