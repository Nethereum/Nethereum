using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Generic;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetBlockTransactionCountByNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
            return await ethGetBlockTransactionCountByNumber.SendRequestAsync( BlockParameter.CreateLatest());
        }

        public Type GetRequestType()
        {
            return typeof(EthGetBlockTransactionCountByNumber);
        }
    }
}
        