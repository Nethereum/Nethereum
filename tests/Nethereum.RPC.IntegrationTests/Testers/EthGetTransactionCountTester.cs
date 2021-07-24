using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionCountTester : RPCRequestTester<HexBigInteger>
    {
        [Fact]
        public async void ShouldReturnTheTransactionCountOfTheAccount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGetTransactionCount = new EthGetTransactionCount(client);
            return await ethGetTransactionCount.SendRequestAsync(Settings.GetDefaultAccount());
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionCount);
        }
    }
}