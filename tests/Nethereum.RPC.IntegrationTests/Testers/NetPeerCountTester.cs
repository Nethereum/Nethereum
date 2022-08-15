using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Tests.Testers
{
    public class NetPeerCountTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var netPeerCount = new NetPeerCount(client);
            return await netPeerCount.SendRequestAsync().ConfigureAwait(false);
        }

        public Type GetRequestType()
        {
            return typeof (NetPeerCount);
        }
    }
}