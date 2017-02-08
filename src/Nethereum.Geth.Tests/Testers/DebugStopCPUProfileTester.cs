using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugStopCPUProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugStopCPUProfile = new DebugStopCPUProfile(client);
            return await debugStopCPUProfile.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStopCPUProfile);
        }
    }
}
        