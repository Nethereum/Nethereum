

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

    public class TxpoolPantheonTransactionsTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var txpoolPantheonTransactions = new TxpoolPantheonTransactions(client);
            return await txpoolPantheonTransactions.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(TxpoolPantheonTransactions);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        