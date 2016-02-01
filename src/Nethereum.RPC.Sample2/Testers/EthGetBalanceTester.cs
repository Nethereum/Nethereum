
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetBalanceTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBalance = new EthGetBalance();
            return await ethGetBalance.SendRequestAsync(client, "0x12890d2cce102216644c59dae5baed380d84830c");
        }

        public Type GetRequestType()
        {
            return typeof(EthGetBalance);
        }
    }
}
        