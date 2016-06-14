
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.RPC.DebugGeth;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugVmoduleTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldSetTheVerbosityAndReturnNull()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugVmodule = new DebugVmodule(client);
            return await debugVmodule.SendRequestAsync("eth/*/peer.go=6,p2p=5");
        }

        public override Type GetRequestType()
        {
            return typeof(DebugVmodule);
        }
    }
}
        