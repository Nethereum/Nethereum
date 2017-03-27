
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityTransactionsLimitTester : RPCRequestTester<int>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<int> ExecuteAsync(IClient client)
        {
            var parityTransactionsLimit = new ParityTransactionsLimit(client);
            return await parityTransactionsLimit.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityTransactionsLimit);
        }
    }
}
        