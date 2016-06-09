using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthNewBlockFilterTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethNewBlockFilter = new EthNewBlockFilter(client);
            return await ethNewBlockFilter.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthNewBlockFilter);
        }
    }
}