using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetTransactionByBlockHashAndIndexTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetTransactionByBlockHashAndIndex = new EthGetTransactionByBlockHashAndIndex(client);
            return (object)await ethGetTransactionByBlockHashAndIndex.SendRequestAsync( "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331", 0);
        }

        public Type GetRequestType()
        {
            return typeof(EthGetTransactionByBlockHashAndIndex);
        }
    }
}
        