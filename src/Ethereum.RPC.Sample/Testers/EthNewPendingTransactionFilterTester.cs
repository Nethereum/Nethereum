
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthNewPendingTransactionFilterTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethNewPendingTransactionFilter = new EthNewPendingTransactionFilter();
            return await ethNewPendingTransactionFilter.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthNewPendingTransactionFilter);
        }
    }
}
        