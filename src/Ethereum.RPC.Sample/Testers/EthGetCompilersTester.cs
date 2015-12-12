
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetCompilersTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethGetCompilers = new EthGetCompilers();
            return ethGetCompilers.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthGetCompilers);
        }
    }
}
        