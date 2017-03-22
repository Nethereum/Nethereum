
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityMinGasPriceTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var parityMinGasPrice = new ParityMinGasPrice(client);
            return await parityMinGasPrice.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityMinGasPrice);
        }
    }
}
        