using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugCpuProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugCpuProfile = new DebugCpuProfile(client);
            return await debugCpuProfile.SendRequestAsync(Settings.GetDefaultLogLocation(), 10);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugCpuProfile);
        }

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }
    }
}