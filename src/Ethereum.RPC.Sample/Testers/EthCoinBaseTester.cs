
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthCoinBaseTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethCoinBase = new EthCoinBase();
            return ethCoinBase.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthCoinBase);
        }
    }
}
        