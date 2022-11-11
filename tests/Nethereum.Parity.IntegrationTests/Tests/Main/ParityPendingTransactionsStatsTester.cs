using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Main
{
    public class ParityPendingTransactionsStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityPendingTransactionsStats = new ParityPendingTransactionsStats(client);
            return await parityPendingTransactionsStats.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityPendingTransactionsStats);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}