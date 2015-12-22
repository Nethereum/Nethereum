
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthSyncingTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethSyncing = new EthSyncing();
            return await ethSyncing.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthSyncing);
        }
    }
}
        