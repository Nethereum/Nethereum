
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthSyncingTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethSyncing = new EthSyncing();
            return ethSyncing.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthSyncing);
        }
    }
}
        