

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Miner;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class MinerStartTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override Task<bool> ExecuteAsync(IClient client)
        {
            var minerStart = new MinerStart(client);
            return minerStart.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(MinerStart);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        