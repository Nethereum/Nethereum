
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityTransactionsLimitTester : RPCRequestTester<int>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
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
        