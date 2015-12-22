
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetCompilersTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetCompilers = new EthGetCompilers();
            return await ethGetCompilers.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthGetCompilers);
        }
    }
}
        