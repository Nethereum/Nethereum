using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetBlockTransactionCountByNumberTester : RPCRequestTester<HexBigInteger>
    {
        [Fact]
        public async void ShouldReturnTransactionCount()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            //we have configured one transaction at least for this block
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
            return await ethGetBlockTransactionCountByNumber.SendRequestAsync(new BlockParameter(Settings.GetBlockNumber())).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof (EthGetBlockTransactionCountByNumber);
        }
    }
}