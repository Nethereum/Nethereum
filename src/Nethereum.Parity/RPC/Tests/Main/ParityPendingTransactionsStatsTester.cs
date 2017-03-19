
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityPendingTransactionsStatsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityPendingTransactionsStats = new ParityPendingTransactionsStats(client);
            return await parityPendingTransactionsStats.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityPendingTransactionsStats);
        }
    }
}
        