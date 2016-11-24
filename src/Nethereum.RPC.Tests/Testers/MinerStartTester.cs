using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Miner;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class MinerStartTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldStartMiner()
        {
            var result = await ExecuteAsync();
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
        