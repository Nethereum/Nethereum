using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Miner;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class MinerSetGasPriceTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var minerSetGasPrice = new MinerSetGasPrice(client);
            return await minerSetGasPrice.SendRequestAsync(new HexBigInteger(1000));
        }

        public override Type GetRequestType()
        {
            return typeof(MinerSetGasPrice);
        }

        [Fact]
        public async void ShouldSetTheGasPrice()
        {
            var result = await ExecuteAsync();
            Assert.True(result);
        }
    }
}