using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetCodeTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetCode = new EthGetCode(client);
            return await ethGetCode.SendRequestAsync("0x12890d2cce102216644c59dae5baed380d84830c");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetCode);
        }
    }
}