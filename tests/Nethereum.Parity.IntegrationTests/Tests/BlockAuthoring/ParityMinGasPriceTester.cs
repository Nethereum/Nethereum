using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.BlockAuthoring
{
    public class ParityMinGasPriceTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var parityMinGasPrice = new ParityMinGasPrice(client);
            return await parityMinGasPrice.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityMinGasPrice);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}