using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthAccountsTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethAccounts = new EthAccounts(client);
            return await ethAccounts.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthAccounts);
        }
    }
}
        