using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugMemStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnAJObject()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugMemStats = new DebugMemStats(client);
            return await debugMemStats.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugMemStats);
        }
    }
}
        