
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.RPC.DebugGeth;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugStartCPUProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldStartCPUProfileAndAlwaysReturnNull()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugCpuProfile = new DebugStartCPUProfile(client);
            return await debugCpuProfile.SendRequestAsync(@"C:\ProgramData\chocolatey\lib\geth-stable\tools\log.txt");
        }

       
        public override Type GetRequestType()
        {
            return typeof(DebugStartCPUProfile);
        }
    }
}
        