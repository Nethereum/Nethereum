using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthSyncingTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethSyncing = new EthSyncing(client);
            return await ethSyncing.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthSyncing);
        }
    }
}