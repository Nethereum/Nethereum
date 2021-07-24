using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.BlockAuthoring
{
    public class ParityTransactionsLimitTester : RPCRequestTester<int>, IRPCRequestTester
    {
        public override async Task<int> ExecuteAsync(IClient client)
        {
            var parityTransactionsLimit = new ParityTransactionsLimit(client);
            return await parityTransactionsLimit.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityTransactionsLimit);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}