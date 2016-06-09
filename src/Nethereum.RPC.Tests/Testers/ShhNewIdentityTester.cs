using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.RPC.Sample.Testers
{
    public class ShhNewIdentityTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var shhNewIdentity = new ShhNewIdentity(client);
            return await shhNewIdentity.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (ShhNewIdentity);
        }
    }
}