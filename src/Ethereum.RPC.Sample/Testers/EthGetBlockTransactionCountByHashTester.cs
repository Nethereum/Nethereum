
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetBlockTransactionCountByHashTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash();
            return await ethGetBlockTransactionCountByHash.SendRequestAsync(client, "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238");
        }

        public Type GetRequestType()
        {
            return typeof(EthGetBlockTransactionCountByHash);
        }
    }
}
        