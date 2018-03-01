using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Miner;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class MinerStopTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerStop = new MinerStop(client);
            return await minerStop.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(MinerStop);
        }

        [Fact]
        public async void ShouldStopMiner()
        {
            var result = await ExecuteAsync();
            Assert.True(result);
        }
    }
}