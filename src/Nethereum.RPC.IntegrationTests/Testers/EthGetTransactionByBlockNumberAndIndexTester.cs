using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionByBlockNumberAndIndexTester : RPCRequestTester<Transaction>
    {
        public EthGetTransactionByBlockNumberAndIndexTester() : base(TestSettingsCategory.hostedTestNet)
        {

        }

        [Fact]
        public async void ShouldReturnTheTransaction()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            Assert.Equal(Settings.GetTransactionHash(), result.TransactionHash);

        }

        public override async Task<Transaction> ExecuteAsync(IClient client)
        {
            var ethGetTransactionByBlockNumberAndIndex = new EthGetTransactionByBlockNumberAndIndex(client);
            return await ethGetTransactionByBlockNumberAndIndex.SendRequestAsync(new HexBigInteger(Settings.GetBlockNumber()), new HexBigInteger(0));
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionByBlockNumberAndIndex);
        }
    }

}