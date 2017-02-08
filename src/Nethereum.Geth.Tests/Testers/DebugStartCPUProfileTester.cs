using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugStartCPUProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldStartCPUProfileAndAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugCpuProfile = new DebugStartCPUProfile(client);
            return await debugCpuProfile.SendRequestAsync(Settings.GetDefaultLogLocation());
        }

       
        public override Type GetRequestType()
        {
            return typeof(DebugStartCPUProfile);
        }
    }
}
        