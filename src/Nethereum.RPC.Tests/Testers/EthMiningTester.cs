using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Mining;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthMiningTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethMining = new EthMining(client);
            return await ethMining.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthMining);
        }
    }
}