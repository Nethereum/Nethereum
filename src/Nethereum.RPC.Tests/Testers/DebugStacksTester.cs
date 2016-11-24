using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class DebugStacksTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnStacksAsString()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var debugStacks = new DebugStacks(client);
            return await debugStacks.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStacks);
        }
    }
}
        