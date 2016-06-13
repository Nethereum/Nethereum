
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugGcStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnTheGcStatsAsJObject()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugGcStats = new DebugGcStats(client);
            return await debugGcStats.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugGcStats);
        }
    }
}
        