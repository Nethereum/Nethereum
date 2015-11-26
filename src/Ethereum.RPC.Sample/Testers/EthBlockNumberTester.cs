
using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthBlockNumberTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethBlockNumber = new EthBlockNumber();
            return ethBlockNumber.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthBlockNumber);
        }
    }
}
        