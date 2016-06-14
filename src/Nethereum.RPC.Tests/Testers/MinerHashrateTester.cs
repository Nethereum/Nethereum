
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Sample.Testers
{
    public class MinerHashrateTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldReturnHashRate()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            //Assert.True();
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var minerHashrate = new MinerHashrate(client);
            return await minerHashrate.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(MinerHashrate);
        }
    }
}
        