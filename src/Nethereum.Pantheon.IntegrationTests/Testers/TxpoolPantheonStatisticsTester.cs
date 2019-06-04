

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Txpool;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class TxpoolPantheonStatisticsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var txpoolPantheonStatistics = new TxpoolPantheonStatistics(client);
            return await txpoolPantheonStatistics.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(TxpoolPantheonStatistics);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        