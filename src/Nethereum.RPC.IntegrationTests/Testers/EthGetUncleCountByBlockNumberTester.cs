using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Uncles;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{

    public class EthGetUncleCountByBlockNumberTester : RPCRequestTester<HexBigInteger>
    {
        public EthGetUncleCountByBlockNumberTester():base(TestSettingsCategory.live)
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
            var ethGetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(client);
            return await ethGetUncleCountByBlockNumber.SendRequestAsync(new HexBigInteger(668));
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetUncleCountByBlockNumber);
        }
    }
}