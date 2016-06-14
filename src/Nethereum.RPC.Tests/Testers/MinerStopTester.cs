
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.RPC.Sample.Testers
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
        