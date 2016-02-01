
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
            return (object)await ethGetTransactionByBlockNumberAndIndex.SendRequestAsync(client, new HexBigInteger(20), new HexBigInteger(0));
        }

        public Type GetRequestType()
        {
            return typeof(EthGetTransactionByBlockNumberAndIndex);
        }
    }
}
        