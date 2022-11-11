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
    public class DebugStopCPUProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugStopCPUProfile = new DebugStopCPUProfile(client);
            return await debugStopCPUProfile.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStopCPUProfile);
        }

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.Null(result);
        }
    }
}