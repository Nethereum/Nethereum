
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthMiningTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethMining = new EthMining();
            return await ethMining.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthMining);
        }
    }
}
        