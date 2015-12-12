
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthMiningTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethMining = new EthMining();
            return ethMining.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthMining);
        }
    }
}
        