using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionByHashTester : RPCRequestTester<Transaction>
    {
        [Fact]
        public async void ShouldReturnTheTransaction()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.Equal(Settings.GetTransactionHash(), result.TransactionHash);

        }

        public override async Task<Transaction> ExecuteAsync(IClient client)
        {
            var ethGetTransactionByHash = new EthGetTransactionByHash(client);
            return await ethGetTransactionByHash.SendRequestAsync(Settings.GetTransactionHash()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionByHash);
        }
    }
}