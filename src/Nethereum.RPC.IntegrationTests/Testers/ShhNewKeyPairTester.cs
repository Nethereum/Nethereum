using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhNewKeyPairTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var shhNewKeyPair = new ShhNewKeyPair(client);
            return await shhNewKeyPair.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (ShhNewKeyPair);
        }
    }
}