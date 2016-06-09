using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthProtocolVersionTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethProtocolVersion = new EthProtocolVersion(client);
            return await ethProtocolVersion.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthProtocolVersion);
        }
    }
}