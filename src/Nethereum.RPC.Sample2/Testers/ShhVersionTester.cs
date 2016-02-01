
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Shh;

namespace Ethereum.RPC.Sample.Testers
{
    public class ShhVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var shhVersion = new ShhVersion();
            return await shhVersion.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(ShhVersion);
        }
    }
}
        