using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetTransactionByHashTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetTransactionByHash = new EthGetTransactionByHash(client);
            return
                await
                    ethGetTransactionByHash.SendRequestAsync(
                        "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetTransactionByHash);
        }
    }

    public class EthGetTransactionReceiptTester : RPCRequestTester<TransactionReceipt>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldRetrieveAccounts()
        {
            var receipt = await ExecuteAsync();
            Assert.NotNull(receipt);
        }

        public override async Task<TransactionReceipt> ExecuteAsync(IClient client)
        {
            var ethGetTransactionByHash = new EthGetTransactionReceipt(client);
            return
                await
                    ethGetTransactionByHash.SendRequestAsync(
                        "0x040554dc82f845d6c44e1d7f3b2109f6ae13a8a3d235529e790829f93d69eb0e");
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetTransactionReceipt);
        }
    }
}