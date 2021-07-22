using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugMemStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugMemStats = new DebugMemStats(client);
            return await debugMemStats.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugMemStats);
        }

        [Fact]
        public async void ShouldReturnAJObject()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}