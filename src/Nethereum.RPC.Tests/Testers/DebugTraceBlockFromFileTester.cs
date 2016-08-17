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
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.Null(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceBlockFromFile = new DebugTraceBlockFromFile(client);
            return await debugTraceBlockFromFile.SendRequestAsync(@"C:\ProgramData\chocolatey\lib\geth-stable\tools\log.txt");
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlockFromFile);
        }
    }
}
        