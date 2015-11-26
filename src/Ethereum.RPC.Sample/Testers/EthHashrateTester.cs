
using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthHashrateTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethHashrate = new EthHashrate();
            return ethHashrate.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthHashrate);
        }
    }
}
        