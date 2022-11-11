using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Miner;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class MinerStopTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerStop = new MinerStop(client);
            return await minerStop.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(MinerStop);
        }

        [Fact]
        public async void ShouldStopMiner()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.True(result);
        }
    }
}