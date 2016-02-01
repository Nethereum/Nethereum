using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetCompilersTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetCompilers = new EthGetCompilers(client);
            return await ethGetCompilers.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthGetCompilers);
        }
    }
}
        