using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Miner;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class MinerStopTester : RPCRequestTester<bool>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldStopMiner()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerStop = new MinerStop(client);
            return await minerStop.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(MinerStop);
        }
    }
}
        