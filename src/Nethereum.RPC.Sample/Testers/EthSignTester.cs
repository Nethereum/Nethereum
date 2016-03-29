using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthSignTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethSign = new EthSign(client);
            return await ethSign.SendRequestAsync("0x12890d2cce102216644c59dae5baed380d84830c", "Hello world");
        }

        public Type GetRequestType()
        {
            return typeof (EthSign);
        }
    }
}