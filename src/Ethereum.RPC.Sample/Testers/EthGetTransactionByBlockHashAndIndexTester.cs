
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetTransactionByBlockHashAndIndexTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetTransactionByBlockHashAndIndex = new EthGetTransactionByBlockHashAndIndex();
            return (object)await ethGetTransactionByBlockHashAndIndex.SendRequestAsync(client, "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331", 0);
        }

        public Type GetRequestType()
        {
            return typeof(EthGetTransactionByBlockHashAndIndex);
        }
    }
}
        