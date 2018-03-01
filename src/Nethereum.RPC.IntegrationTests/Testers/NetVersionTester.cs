using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.RPC.Tests.Testers
{
    public class NetVersionTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
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