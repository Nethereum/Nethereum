using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthNewPendingTransactionFilterTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethNewPendingTransactionFilter = new EthNewPendingTransactionFilter(client);
            return await ethNewPendingTransactionFilter.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthNewPendingTransactionFilter);
        }
    }
}
        