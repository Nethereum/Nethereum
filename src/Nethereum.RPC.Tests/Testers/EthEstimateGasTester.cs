using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthEstimateGasTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethEstimateGas = new EthEstimateGas(client);
            var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
            var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";

            var transactionInput = new CallInput();
            transactionInput.Data = contractByteCode;
            transactionInput.To = to;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";

            return await ethEstimateGas.SendRequestAsync(transactionInput);
        }

        public Type GetRequestType()
        {
            return typeof (EthEstimateGas);
        }
    }
}