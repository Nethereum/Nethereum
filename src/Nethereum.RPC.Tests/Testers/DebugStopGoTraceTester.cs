
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.RPC.DebugGeth;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugStopGoTraceTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
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
        