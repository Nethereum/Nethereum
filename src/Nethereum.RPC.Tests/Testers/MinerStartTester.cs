
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.RPC.Sample.Testers
{
    public class MinerStartTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldStartMiner()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerStart = new MinerStart(client);
            return await minerStart.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(MinerStart);
        }
    }
}
        