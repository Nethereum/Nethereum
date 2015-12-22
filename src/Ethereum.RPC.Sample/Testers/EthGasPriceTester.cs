
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGasPriceTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGasPrice = new EthGasPrice();
            return await ethGasPrice.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthGasPrice);
        }
    }
}
        