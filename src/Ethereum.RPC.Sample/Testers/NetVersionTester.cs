
using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetVersionTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var netVersion = new NetVersion();
            return netVersion.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(NetVersion);
        }
    }
}
        