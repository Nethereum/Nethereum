using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Mining;
using Nethereum.RPC.Miner;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthHashrateTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnHashRate()
        {
            var result = await ExecuteAsync();
            //We should not be mining so hash rate is 0
            Assert.Equal(result.HexValue, "0x0");
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var minerHashrate = new EthHashrate(client);
            return await minerHashrate.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthHashrate);
        }
    }
}