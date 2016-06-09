using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetCompilersTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetCompilers = new EthGetCompilers(client);
            return await ethGetCompilers.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthGetCompilers);
        }
    }
}