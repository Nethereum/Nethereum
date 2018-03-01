using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugStartGoTraceTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugStartGoTrace = new DebugStartGoTrace(client);
            return await debugStartGoTrace.SendRequestAsync(Settings.GetDefaultLogLocation(), 30);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStartGoTrace);
        }

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }
    }
}