
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Miner;

namespace Nethereum.RPC.Sample.Testers
{
    public class MinerSetGasPriceTester : RPCRequestTester<bool>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldSetTheGasPrice()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerSetGasPrice = new MinerSetGasPrice(client);
            return await minerSetGasPrice.SendRequestAsync(new HexBigInteger(1000));
        }

        public override Type GetRequestType()
        {
            return typeof(MinerSetGasPrice);
        }
    }
}
        