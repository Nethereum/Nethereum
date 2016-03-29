using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.RPC.Sample.Testers
{
    public class ShhVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var shhVersion = new ShhVersion(client);
            return await shhVersion.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (ShhVersion);
        }
    }
}