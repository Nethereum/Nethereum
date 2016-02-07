using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthExecuteFunctionTester : IRPCRequestTester
    {
        /*

         curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendTransaction","params":[{"from":"0x65180b8c813457b21dad6cc6363d195231b4d2e9","data":"0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056"}],"id":1}' http://localhost:8545

         */
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
            var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";
            var ethSendTransation = new EthSendTransaction(client);
            var transactionInput = new TransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.To = to;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
            return await ethSendTransation.SendRequestAsync( transactionInput);

        }
        public Type GetRequestType()
        {
            return typeof(EthExecuteFunctionTester);
        }
    }
}