using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetTransactionCountTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetTransactionCount = new EthGetTransactionCount(client);
            return await ethGetTransactionCount.SendRequestAsync("0x12890d2cce102216644c59dae5baed380d84830c");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetTransactionCount);
        }
    }
}