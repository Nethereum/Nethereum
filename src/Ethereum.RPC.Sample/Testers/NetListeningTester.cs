
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Net;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetListeningTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var netListening = new NetListening();
            return netListening.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(NetListening);
        }
    }
}
        