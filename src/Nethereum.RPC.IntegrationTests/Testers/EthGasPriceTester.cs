using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGasPriceTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        public EthGasPriceTester() : base(TestSettingsCategory.hostedTestNet)
        {
        }

        [Fact]
        public async void ShouldGetTheGasPrice()
        {
            var result = await ExecuteAsync();
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGasPrice = new EthGasPrice(client);
            return await ethGasPrice.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthGasPrice);
        }
    }
}