using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthBlockNumberTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldRetrieveBlockNumberBiggerThanZero()
        {
            var blockNumber = await ExecuteAsync(ClientFactory.GetClient());
            Assert.True(blockNumber.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethBlockNumber = new EthBlockNumber(client);
            return await ethBlockNumber.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof (EthBlockNumber);
        }
    }
}