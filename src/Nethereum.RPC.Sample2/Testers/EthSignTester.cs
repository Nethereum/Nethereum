
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthSignTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethSign = new EthSign();
            return await ethSign.SendRequestAsync(client, "0x12890d2cce102216644c59dae5baed380d84830c", "Hello world");
        }

        public Type GetRequestType()
        {
            return typeof(EthSign);
        }
    }
}
        