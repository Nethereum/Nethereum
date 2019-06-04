

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Miner;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
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
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        