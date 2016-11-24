using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugStopGoTraceTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugStopGoTrace = new DebugStopGoTrace(client);
            return await debugStopGoTrace.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStopGoTrace);
        }
    }
}
        