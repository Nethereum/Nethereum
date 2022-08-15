using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class PersonalUnlLockAccountTester : RPCRequestTester<bool>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldUnLockAccountAndReturnTrue()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var personalunlockAccount = new PersonalUnlockAccount(client);
            ulong? duration = null;
            await personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(), Settings.GetDefaultAccountPassword(), duration).ConfigureAwait(false);
            if (Settings.IsParity())
            {
                //Parity
                return
                    await
                        personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(),
                            Settings.GetDefaultAccountPassword(), new HexBigInteger(30)).ConfigureAwait(false);
            }
            else
            {
                return
                    await
                        personalunlockAccount.SendRequestAsync(Settings.GetDefaultAccount(),
                            Settings.GetDefaultAccountPassword(), 30).ConfigureAwait(false);
            }
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalUnlockAccount);
        }
    }
}