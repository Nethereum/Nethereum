using System;
using System.Numerics;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Sample.Testers;

namespace Nethereum.RPC.Sample.ContractTest
{
    public class DeployContractCallFunctionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {

            
            //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode = "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            //Create a new Eth Send Transanction RPC Handler
            var ethSendTransation = new EthSendTransaction(client);
            //As the input the compiled contract is the Data, together with our address
            var transactionInput = new EthSendTransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
            // retrieve the hash
            var transactionHash =  await ethSendTransation.SendRequestAsync( transactionInput);
            
            //the contract should be mining now

            //get contract address 
            var ethGetTransactionReceipt = new EthGetTransactionReceipt(client);
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = await ethGetTransactionReceipt.SendRequestAsync( transactionHash);
            }

            //Encode and build function parameters 
            var function = new FunctionCallEncoder();

            //Input the function method Sha3Encoded (4 bytes) 
            var sha3Signature = "c6888fa1";
            //Define input parameters
            var inputParameters = new[] { new Parameter("uint", "a") };
            //encode the function call (function + parameter input)
           
            //using 69 as the input
            var functionCall = function.EncodeRequest(sha3Signature, inputParameters, 69);
            //reuse the transaction input, (just the address) 
            //the destination address is the contract address
            transactionInput.To = receipt.ContractAddress;
            //use as data the function call
            transactionInput.Data = functionCall;
            // rpc method to do the call
            var ethCall = new EthCall(client);
            // call and get the result
            var resultFunction = await ethCall.SendRequestAsync( transactionInput);
            // decode the output
            var functionDecoder = new FunctionCallDecoder();
            var output =  (BigInteger)functionDecoder.DecodeOutput(resultFunction, new Parameter("uint"))[0].Result;
            //visual test 
            return "The result of deploying a contract and calling a function to multiply 7 by 69 is: " + (int)output  + " and should be 483";

           

        }
        public Type GetRequestType()
        {
            return typeof(DeployContractCallFunctionTester);
        }
    }
}