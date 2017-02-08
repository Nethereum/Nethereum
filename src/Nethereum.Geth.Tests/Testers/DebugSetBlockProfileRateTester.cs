using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugSetBlockProfileRateTester : RPCRequestTester<object>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldSetBlockProfileAndReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }

        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugSetBlockProfileRate = new DebugSetBlockProfileRate(client);
            return await debugSetBlockProfileRate.SendRequestAsync(10);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugSetBlockProfileRate);
        }
    }
}
        