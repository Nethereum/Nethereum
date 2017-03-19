
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityLocalTransactionsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityLocalTransactions = new ParityLocalTransactions(client);
            return await parityLocalTransactions.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityLocalTransactions);
        }
    }
}
        