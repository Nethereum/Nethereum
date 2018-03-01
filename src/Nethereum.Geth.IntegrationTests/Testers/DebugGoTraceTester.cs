using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugGoTraceTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugGoTrace = new DebugGoTrace(client);
            return await debugGoTrace.SendRequestAsync(Settings.GetDefaultLogLocation(), 10);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugGoTrace);
        }

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }
    }
}