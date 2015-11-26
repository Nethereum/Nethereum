
using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample.Testers
{
    public class ShhNewIdentityTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var shhNewIdentity = new ShhNewIdentity();
            return shhNewIdentity.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(ShhNewIdentity);
        }
    }
}
        