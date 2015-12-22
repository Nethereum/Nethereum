
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Net;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetPeerCountTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netPeerCount = new NetPeerCount();
            return await netPeerCount.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(NetPeerCount);
        }
    }
}
        