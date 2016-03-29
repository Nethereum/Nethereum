using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Sample.Testers
{
    public class NetVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netVersion = new NetVersion(client);
            return await netVersion.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (NetVersion);
        }
    }
}