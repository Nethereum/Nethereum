using edjCase.JsonRpc.Client;
using Ethereum.RPC.SendTransaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample
{
    public class Program
    {
        static void Main(string[] args)
        {
           
            RpcClient client = new RpcClient(new Uri("http://localhost:8545/"));
            var web3ClientVersion = new Web3ClientVersion();
            var result = web3ClientVersion.SendRequestAsync(client).Result;
            ConsoleOutputResult(result, web3ClientVersion.GetType());

            var web3Sha3 = new Web3Sha3();
            result = web3Sha3.SendRequestAsync(client, "Monkey").Result;
            ConsoleOutputResult(result, web3Sha3.GetType());


            
            var ethCompileSolidty = new EthCompileSolidity();
            try
            {
                var contractCode = "contract test { function multiply(uint a) returns(uint d) { return a * 7; } }";
                result = ethCompileSolidty.SendRequestAsync(client, contractCode).Result;
                ConsoleOutputResult(result, ethCompileSolidty.GetType());
            }catch(Exception ex)
            {
                ConsoleOutputResult("Error:" + ex.Message, ethCompileSolidty.GetType());
            }

            /*

            curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendTransaction","params":[{"from":"0x65180b8c813457b21dad6cc6363d195231b4d2e9","data":"0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056"}],"id":1}' http://localhost:8545

            */

            var contractByteCode = "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";
            var ethSendTransation = new EthSendTransaction();
            var transactionInput = new TransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x65180b8c813457b21dad6cc6363d195231b4d2e9";
            try { 
            result = ethSendTransation.SendRequestAsync(client, transactionInput).Result;
            ConsoleOutputResult(result, ethSendTransation.GetType());

            } catch(Exception ex)
            {
                ConsoleOutputResult("Error:" + ex.Message, ethCompileSolidty.GetType());
            }


            Console.ReadLine();

        }

        public static void ConsoleOutputResult(object result, Type methodType)
        {
            Console.WriteLine();
            Console.Write(methodType.ToString());
            Console.WriteLine();
            Console.Write("------------------------");
            Console.WriteLine();
            if(result != null)
            Console.Write(result.ToString());
            Console.WriteLine();
        }
    }
}
