using edjCase.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Net45
{
    class Program
    {
        static void Main(string[] args)
        {
            RpcClient client = new RpcClient(new Uri("http://localhost:8545/"));
            var web3ClientVersion = new Web3ClientVersion();
            var result = web3ClientVersion.SendRequestAsync(client).Result;
            ConsoleOutputResult(result, web3ClientVersion.GetType());

            var web3Sha3 = new Web3Sha3();
            result =  web3Sha3.SendRequestAsync(client, "Monkey").Result;
            ConsoleOutputResult(result, web3Sha3.GetType());

            Console.ReadLine();

        }

        public static void ConsoleOutputResult(object result, Type methodType)
        {
            Console.WriteLine();
            Console.Write(methodType.ToString());
            Console.WriteLine();
            Console.Write("------------------------");
            Console.WriteLine();
            Console.Write(result);
            Console.WriteLine();
        }
    }
}
