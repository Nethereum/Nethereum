using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugDumpBlockTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnAJObject()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugDumpBlock = new DebugDumpBlock(client);
            return await debugDumpBlock.SendRequestAsync(Settings.GetBlockNumber());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugDumpBlock);
        }
    }
}
        