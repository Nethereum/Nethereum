
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthNewBlockFilterTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethNewBlockFilter = new EthNewBlockFilter();
            return await ethNewBlockFilter.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthNewBlockFilter);
        }
    }
}
        