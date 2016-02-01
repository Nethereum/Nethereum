
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetBlockByHashTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBlockByHash = new EthGetBlockWithTransactionsByHash();
            return await ethGetBlockByHash.SendRequestAsync(client, "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331");
        }

        public Type GetRequestType()
        {
            return typeof(EthGetBlockWithTransactionsByHash);
        }
    }
}
        