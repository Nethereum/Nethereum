using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Tests.Testers
{
    public class NetListeningTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
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