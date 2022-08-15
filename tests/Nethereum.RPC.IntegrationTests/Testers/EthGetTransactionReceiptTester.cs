using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionReceiptTester : RPCRequestTester<TransactionReceipt>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldRetrieveReceipt()
        {
            var receipt = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(receipt);
        }

        public override async Task<TransactionReceipt> ExecuteAsync(IClient client)
        {
            var ethGetTransactionByHash = new EthGetTransactionReceipt(client);
            return
                await
                    ethGetTransactionByHash.SendRequestAsync(
                        Settings.GetTransactionHash()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionReceipt);
        }
    }
}