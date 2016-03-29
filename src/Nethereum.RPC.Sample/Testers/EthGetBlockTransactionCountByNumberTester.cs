using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetBlockTransactionCountByNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
            return await ethGetBlockTransactionCountByNumber.SendRequestAsync(BlockParameter.CreateLatest());
        }

        public Type GetRequestType()
        {
            return typeof (EthGetBlockTransactionCountByNumber);
        }
    }
}