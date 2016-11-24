using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugBlockProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugBlockProfile = new DebugBlockProfile(client);
            return await debugBlockProfile.SendRequestAsync(Settings.GetDefaultLogLocation(), 30);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugBlockProfile);
        }
    }
}
        