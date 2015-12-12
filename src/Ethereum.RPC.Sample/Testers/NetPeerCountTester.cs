
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Net;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetPeerCountTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var netPeerCount = new NetPeerCount();
            return netPeerCount.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(NetPeerCount);
        }
    }
}
        