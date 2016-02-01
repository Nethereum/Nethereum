
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Net;

namespace Ethereum.RPC.Sample.Testers
{
    public class NetListeningTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netListening = new NetListening();
            return await netListening.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(NetListening);
        }
    }
}
        