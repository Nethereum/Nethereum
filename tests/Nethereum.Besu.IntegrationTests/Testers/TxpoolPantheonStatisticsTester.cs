

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Txpool;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class TxpoolBesuStatisticsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override Task<JObject> ExecuteAsync(IClient client)
        {
            var txpoolBesuStatistics = new TxpoolBesuStatistics(client);
            return txpoolBesuStatistics.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(TxpoolBesuStatistics);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        