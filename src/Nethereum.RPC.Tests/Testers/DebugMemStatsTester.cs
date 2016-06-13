
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugMemStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnAJObject()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
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
        