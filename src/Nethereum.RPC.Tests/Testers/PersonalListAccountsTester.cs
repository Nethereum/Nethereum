using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class PersonalListAccountsTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
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
        