using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceBlockFromFileTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        //[Fact] TODO: Refactor test
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
        