
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.RPC.Personal;

namespace Nethereum.RPC.Sample.Testers
{
    public class PersonalLockAccountTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldLockAccountAndReturnTrue()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var personalLockAccount = new PersonalLockAccount(client);
            return await personalLockAccount.SendRequestAsync("0x12890d2cce102216644c59dae5baed380d84830c");
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalLockAccount);
        }
    }
}
        