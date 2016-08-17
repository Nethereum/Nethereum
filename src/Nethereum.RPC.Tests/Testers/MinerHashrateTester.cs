using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Miner;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
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
        