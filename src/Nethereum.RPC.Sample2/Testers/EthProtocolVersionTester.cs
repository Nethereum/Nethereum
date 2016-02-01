
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthProtocolVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethProtocolVersion = new EthProtocolVersion();
            return await ethProtocolVersion.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthProtocolVersion);
        }
    }
}
        