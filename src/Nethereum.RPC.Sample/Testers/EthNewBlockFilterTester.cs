using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthNewBlockFilterTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethNewBlockFilter = new EthNewBlockFilter(client);
            return await ethNewBlockFilter.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthNewBlockFilter);
        }
    }
}
        