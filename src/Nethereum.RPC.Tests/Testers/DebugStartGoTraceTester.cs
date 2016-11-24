using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugStartGoTraceTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugStartGoTrace = new DebugStartGoTrace(client);
            return await debugStartGoTrace.SendRequestAsync(Settings.GetDefaultLogLocation(), 30);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStartGoTrace);
        }
    }
}
        