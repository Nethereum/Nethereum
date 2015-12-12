
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthAccountsTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethAccounts = new EthAccounts();
            return ethAccounts.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthAccounts);
        }
    }
}
        