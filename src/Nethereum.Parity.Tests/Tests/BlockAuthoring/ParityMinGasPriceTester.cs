
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Hex.HexTypes;
using Nethereum.Parity.RPC.BlockAuthoring;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityMinGasPriceTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
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
        