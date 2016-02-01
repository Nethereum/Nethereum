
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Shh;

namespace Ethereum.RPC.Sample.Testers
{
    public class ShhNewIdentityTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var shhNewIdentity = new ShhNewIdentity();
            return await shhNewIdentity.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(ShhNewIdentity);
        }
    }
}
        