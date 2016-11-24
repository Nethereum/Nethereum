using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugGcStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnTheGcStatsAsJObject()
        {
            var result = await ExecuteAsync();
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
        