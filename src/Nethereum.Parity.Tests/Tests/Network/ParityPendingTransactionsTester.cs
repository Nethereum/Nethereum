
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityPendingTransactionsTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var parityPendingTransactions = new ParityPendingTransactions(client);
            return await parityPendingTransactions.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityPendingTransactions);
        }
    }
}
        