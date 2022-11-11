using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
// ReSharper disable AsyncConverter.AsyncAwaitMayBeElidedHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugBlockProfileTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugBlockProfile = new DebugBlockProfile(client);
            
            return await debugBlockProfile.SendRequestAsync(Settings.GetDefaultLogLocation(), 30).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugBlockProfile);
        }

        [Fact]
        public async void ShouldAlwaysReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.Null(result);
        }
    }
}