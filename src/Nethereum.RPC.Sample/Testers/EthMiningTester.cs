using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Mining;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthMiningTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethMining = new EthMining(client);
            return await ethMining.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthMining);
        }
    }
}
        