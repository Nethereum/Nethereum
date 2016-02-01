
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthAccountsTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethAccounts = new EthAccounts();
            return await ethAccounts.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthAccounts);
        }
    }
}
        