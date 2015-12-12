using edjCase.JsonRpc.Client;
using RPCRequestResponseHandlers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;
using Ethereum.RPC.Web3;

namespace Ethereum.RPC.Sample
{
    public class Program
    {


        private static void Main(string[] args)
        {

            RpcClient client = new RpcClient(new Uri("http://localhost:8545/"));

            Type testerType = typeof (IRPCRequestTester);
            var assembly = testerType.GetTypeInfo().Assembly;
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (typeof (IRPCRequestTester).IsAssignableFrom(type) && type != typeof (IRPCRequestTester))
                {
                    var tester = (IRPCRequestTester) Activator.CreateInstance(type);
                    try
                    {

                        var testerResult = tester.ExecuteTest(client);
                        ConsoleOutputResult(testerResult, tester.GetRequestType());
                    }
                    catch (Exception ex)
                    {
                        ConsoleOutputResult("Error:" + ex.InnerException.Message, tester.GetRequestType());
                    }
                }
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
            if (result != null)
            {
                IEnumerable array = result as IEnumerable;
                if (array != null && !(result is String))
                {
                    foreach (var item in array)
                    {
                        Console.WriteLine(ConvertToString(item));
                    }
                }
                else
                {
                    Console.Write(ConvertToString(result));
                }
            }
            Console.WriteLine();
        }

        public static string ConvertToString(object input)
        {
            if (IsSimpleType(input.GetType())) return input.ToString();

            var props = input.GetType().GetProperties();
            if (props.Length > 0)
            {
                var sb = new StringBuilder();
                foreach (var p in props)
                {
                    sb.AppendLine(p.Name + ": " + p.GetValue(input, null));
                }
                return sb.ToString();
            }
            else
            {
                return input.ToString();
            }

        }

        public static bool IsSimpleType(Type type)
        {
            return type.GetTypeInfo().IsPrimitive || type.GetTypeInfo().IsEnum || type == typeof (string) ||
                   type == typeof (decimal);
        }

    }

    public class Web3Sha3Tester : IRPCRequestTester
        {
            public dynamic ExecuteTest(RpcClient client)
            {
                var web3Sha3 = new Web3Sha3();
                return web3Sha3.SendRequestAsync(client, "Monkey").Result;
            }

            public Type GetRequestType()
            {
                return typeof(Web3Sha3Tester);
            }
        }

        public class EthCompileSolidityTester : IRPCRequestTester
        {
            public dynamic ExecuteTest(RpcClient client)
            {
                var ethCompileSolidty = new EthCompileSolidity();
                var contractCode = "contract test { function multiply(uint a) returns(uint d) { return a * 7; } }";
                return ethCompileSolidty.SendRequestAsync(client, contractCode).Result;
            }

            public Type GetRequestType()
            {
                return typeof(EthCompileSolidity);
            }
        }

        //0xcb047ad59c623b7c76226b7df0afda258847bd96eaf4089363644f3db280d6eb
        public class EthExecuteFunctionTester : IRPCRequestTester
        {
            /*

         curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendTransaction","params":[{"from":"0x65180b8c813457b21dad6cc6363d195231b4d2e9","data":"0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056"}],"id":1}' http://localhost:8545

         */
            public dynamic ExecuteTest(RpcClient client)
            {
                var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
                var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";
                var ethSendTransation = new EthSendTransaction();
                var transactionInput = new EthSendTransactionInput();
                transactionInput.Data = contractByteCode;
                transactionInput.To = to;
                transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
                return ethSendTransation.SendRequestAsync(client, transactionInput).Result;

            }
            public Type GetRequestType()
            {
                return typeof(EthExecuteFunctionTester);
            }
        }


        public class EthCallTester : IRPCRequestTester
        {
            /*

         curl -X POST --data '{"jsonrpc":"2.0","method":"eth_call","params":[{"from":"0x65180b8c813457b21dad6cc6363d195231b4d2e9","data":"0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056"}],"id":1}' http://localhost:8545

         */
            public dynamic ExecuteTest(RpcClient client)
            {
                //function multiply , input 69
                var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
                var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";
                var ethSendTransation = new EthCall();
                var transactionInput = new EthSendTransactionInput();
                transactionInput.Data = contractByteCode;
                transactionInput.To = to;
                transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
                return ethSendTransation.SendRequestAsync(client, transactionInput).Result;
                //result
                // 0x00000000000000000000000000000000000000000000000000000000000001e3
                //483
            }
            public Type GetRequestType()
            {
                return typeof(EthCallTester);
            }
        }

        public class EthSendTransactionTester : IRPCRequestTester
        {
            /*

         curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendTransaction","params":[{"from":"0x65180b8c813457b21dad6cc6363d195231b4d2e9","data":"0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056"}],"id":1}' http://localhost:8545

         */
            public dynamic ExecuteTest(RpcClient client)
            {
                var contractByteCode = "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";
                var ethSendTransation = new EthSendTransaction();
                var transactionInput = new EthSendTransactionInput();
                transactionInput.Data = contractByteCode;
                transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
                return ethSendTransation.SendRequestAsync(client, transactionInput).Result;

            }
            public Type GetRequestType()
            {
                return typeof(EthSendTransaction);
            }
        }


    
}
