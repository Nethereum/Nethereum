
using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample.Testers
{
    public class ShhVersionTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var shhVersion = new ShhVersion();
            return shhVersion.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(ShhVersion);
        }
    }
}
        