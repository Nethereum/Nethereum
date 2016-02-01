
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetTransactionByHashTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetTransactionByHash = new EthGetTransactionByHash();
            return await ethGetTransactionByHash.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthGetTransactionByHash);
        }
    }
}
        