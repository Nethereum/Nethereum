
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugGetxBlockRlpTester : RPCRequestTester<string>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldReturnTheBlockRplAsAString()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var debugGetBlockRlp = new DebugGetBlockRlp(client);
            return await debugGetBlockRlp.SendRequestAsync(10);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugGetBlockRlp);
        }
    }
}
        