
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugDumpBlockTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnAJObject()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugDumpBlock = new DebugDumpBlock(client);
            return await debugDumpBlock.SendRequestAsync(10);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugDumpBlock);
        }
    }
}
        