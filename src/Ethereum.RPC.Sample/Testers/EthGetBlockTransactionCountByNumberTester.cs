
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Generic;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetBlockTransactionCountByNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber();
            return await ethGetBlockTransactionCountByNumber.SendRequestAsync(client, BlockParameter.CreateLatest());
        }

        public Type GetRequestType()
        {
            return typeof(EthGetBlockTransactionCountByNumber);
        }
    }
}
        