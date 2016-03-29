using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetBalanceTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetBalance = new EthGetBalance(client);
            return await ethGetBalance.SendRequestAsync("0x12890d2cce102216644c59dae5baed380d84830c");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetBalance);
        }
    }
}