
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetTransactionByBlockNumberAndIndexTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetTransactionByBlockNumberAndIndex = new EthGetTransactionByBlockNumberAndIndex();
            return await ethGetTransactionByBlockNumberAndIndex.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthGetTransactionByBlockNumberAndIndex);
        }
    }
}
        