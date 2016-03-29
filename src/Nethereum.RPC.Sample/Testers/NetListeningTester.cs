using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Sample.Testers
{
    public class NetListeningTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var netListening = new NetListening(client);
            return await netListening.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (NetListening);
        }
    }
}