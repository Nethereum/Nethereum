using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class PersonalLockAccountTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldLockAccountAndReturnTrue()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var personalLockAccount = new PersonalLockAccount(client);
            return await personalLockAccount.SendRequestAsync(Settings.GetDefaultAccount()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalLockAccount);
        }
    }
}
        