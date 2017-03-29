
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityDefaultAccountTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGetDefaultAccount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityDefaultAccount = new ParityDefaultAccount(client);
            return await parityDefaultAccount.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityDefaultAccount);
        }
    }
}
        