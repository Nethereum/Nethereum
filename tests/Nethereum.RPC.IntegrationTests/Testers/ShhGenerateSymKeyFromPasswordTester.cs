using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Shh.SymKey;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhGenerateSymKeyFromPasswordTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnTheSymKey()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var shhNewKeyPair = new ShhGenerateSymKeyFromPassword(client);
            return await shhNewKeyPair.SendRequestAsync("password");
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }
}