using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionByHashTester : RPCRequestTester<Transaction>
    {
        public EthGetTransactionByHashTester() : base(TestSettingsCategory.hostedTestNet)
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
            var ethGetTransactionByHash = new EthGetTransactionByHash(client);
            return await ethGetTransactionByHash.SendRequestAsync(Settings.GetTransactionHash());
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionByHash);
        }
    }
}