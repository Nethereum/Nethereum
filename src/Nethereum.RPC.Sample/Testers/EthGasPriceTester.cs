using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGasPriceTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGasPrice = new EthGasPrice(client);
            return await ethGasPrice.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthGasPrice);
        }
    }
}