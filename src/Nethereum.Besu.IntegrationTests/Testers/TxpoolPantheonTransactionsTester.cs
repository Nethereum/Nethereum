

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

    public class TxpoolBesuTransactionsTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var txpoolBesuTransactions = new TxpoolBesuTransactions(client);
            return await txpoolBesuTransactions.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(TxpoolBesuTransactions);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        