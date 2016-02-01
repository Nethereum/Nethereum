using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthProtocolVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethProtocolVersion = new EthProtocolVersion(client);
            return await ethProtocolVersion.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthProtocolVersion);
        }
    }
}
        