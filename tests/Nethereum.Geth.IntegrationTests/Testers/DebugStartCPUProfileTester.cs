using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugStartCPUProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugCpuProfile = new DebugStartCPUProfile(client);
            return await debugCpuProfile.SendRequestAsync(Settings.GetDefaultLogLocation()).ConfigureAwait(false);
        }


        public override Type GetRequestType()
        {
            return typeof(DebugStartCPUProfile);
        }

        [Fact]
        public async void ShouldStartCPUProfileAndAlwaysReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.Null(result);
        }
    }
}