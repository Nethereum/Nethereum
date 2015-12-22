
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Net;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netVersion = new NetVersion();
            return await netVersion.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(NetVersion);
        }
    }
}
        