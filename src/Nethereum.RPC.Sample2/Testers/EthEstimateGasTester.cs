
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthEstimateGasTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethEstimateGas = new EthEstimateGas();
            var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
            var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";
           
            var transactionInput = new EthCallTransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.To = to;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
           
            return await ethEstimateGas.SendRequestAsync(client, transactionInput);
        }

        public Type GetRequestType()
        {
            return typeof(EthEstimateGas);
        }
    }
}
        