using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Accounts
{
    public class ParityDefaultAccountTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityDefaultAccount = new ParityDefaultAccount(client);
            return await parityDefaultAccount.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityDefaultAccount);
        }

        [Fact]
        public async void ShouldGetDefaultAccount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}