using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugTraceBlockFromFileTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
       [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceBlockFromFile = new DebugTraceBlockFromFile(client);
            return await debugTraceBlockFromFile.SendRequestAsync(Settings.GetDefaultLogLocation());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlockFromFile);
        }
    }
}
        