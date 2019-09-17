using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.Uncles;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
 
    public class EthGetUncleCountByBlockHashTester : RPCRequestTester<HexBigInteger>
    {

        public EthGetUncleCountByBlockHashTester():base(TestSettings.LiveSettings)
        {
        }

        [Fact]
        public async void ShouldReturnTheUncleCount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            Assert.Equal(1, result.Value);

        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            return await ethGetUncleCountByBlockHash.SendRequestAsync("0x84e538e6da2340e3d4d90535f334c22974fecd037798d1cf8965c02e8ab3394b");
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetUncleCountByBlockHash);
        }
    }
}