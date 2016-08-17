using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthAccountsTester : RPCRequestTester<String[]>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldRetrieveAccounts()
        {
            var accounts = await ExecuteAsync(ClientFactory.GetClient());
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