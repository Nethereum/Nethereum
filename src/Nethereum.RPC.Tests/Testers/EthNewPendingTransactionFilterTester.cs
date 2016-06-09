using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthNewPendingTransactionFilterTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethNewPendingTransactionFilter = new EthNewPendingTransactionFilter(client);
            return await ethNewPendingTransactionFilter.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthNewPendingTransactionFilter);
        }
    }
}