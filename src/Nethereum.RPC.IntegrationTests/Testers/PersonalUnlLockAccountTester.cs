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
            base(ethereumClientIntegrationFixture, TestSettings.GethLocalSettings)
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
            await personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(), Settings.GetDefaultAccountPassword(), duration);
            if (Settings.IsParity())
            {
                //Parity
                return
                    await
                        personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(),
                            Settings.GetDefaultAccountPassword(), new HexBigInteger(30));
            }
            else
            {
                return
                    await
                        personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(),
                            Settings.GetDefaultAccountPassword(), 30);
            }
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalUnlockAccount);
        }
    }
}