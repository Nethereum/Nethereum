using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Sample.Testers
{
    public class NetPeerCountTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netPeerCount = new NetPeerCount(client);
            return await netPeerCount.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(NetPeerCount);
        }
    }
}
        