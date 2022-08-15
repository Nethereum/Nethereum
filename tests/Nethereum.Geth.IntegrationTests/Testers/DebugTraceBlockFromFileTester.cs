using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceBlockFromFileTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlockFromFile);
        }

        //[Fact] TODO: Refactor test
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.Null(result);
        }

        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var debugTraceBlockFromFile = new DebugTraceBlockFromFile(client);
            return await debugTraceBlockFromFile.SendRequestAsync(Settings.GetDefaultLogLocation(), new TraceTransactionOptions()).ConfigureAwait(false);
        }
    }
}