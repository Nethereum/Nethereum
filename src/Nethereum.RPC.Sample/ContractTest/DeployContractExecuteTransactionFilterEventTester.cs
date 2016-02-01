using System;
using System.Text;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Generic;
using Nethereum.RPC.Sample.Testers;

namespace Nethereum.RPC.Sample.ContractTest
{
    public class DeployContractExecuteTransactionFilterEventTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            /* This is the example contract containing an event raised every time we call multiply
            contract test { 
    
                event Multiplied(uint indexed a, address sender);
    
                function multiply(uint a) returns(uint d) 
                { 
                    Multiplied(a, msg.sender);
                    return a * 7; 
                    
                } 
    
            }*/

            //The contract byte code already compiled
            var contractByteCode = "606060405260c08060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000817f10f82b5dc139f3677a16d7bfb70c65252e78143313768d2c52e07db775e1c7ab33604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a260078202905060bb565b91905056";
            
            //Create a new Eth Send Transanction RPC Handler
            var ethSendTransation = new EthSendTransaction(client);

            //Create the transaction input for the new contract

            //On transaction input the compiled contract is the Data, together with our sender address 
            var transactionInput = new EthSendTransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
            // retrieve the transaction hash, as we need to get a transaction receipt with the contract address
            var transactionHash = await ethSendTransation.SendRequestAsync( transactionInput);

            //the contract should be mining now

            //Get the transaction receipt using the transactionHash
            var ethGetTransactionReceipt = new EthGetTransactionReceipt(client);
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = await ethGetTransactionReceipt.SendRequestAsync( transactionHash);
            }

            //sha3 the event call, we can use this to validate our topics 
            
            var eventCallSh3 = await new Web3.Web3Sha3(client).SendRequestAsync( new HexString("Multiplied(uint256,address)"));
            //create a filter 
            //just listen to anything no more filter topics (ie int indexed number)
            var ethFilterInput = new EthNewFilterInput();
            ethFilterInput.FromBlock.SetValue(receipt.BlockNumber);
            ethFilterInput.ToBlock = BlockParameter.CreateLatest();
            ethFilterInput.Address = new [] { receipt.ContractAddress};
           //no topics
            //ethFilterInput.Topics = new object[]{};

            var newEthFilter = new EthNewFilter(client);
            var filterId = await newEthFilter.SendRequestAsync( ethFilterInput);

           
            //create a transaction which will raise the event
            await SendTransaction(client, transactionInput.From, receipt.ContractAddress);

            //get filter changes
            var ethGetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter(client);
            EthNewFilterLog[] logs = null;

            while (logs == null || logs.Length < 1)
            {
                //Get the filter changes logs
                logs = await ethGetFilterChangesForEthNewFilter.SendRequestAsync( filterId);

                if (logs.Length > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Topic 0: " + logs[0].Topics[0] + " should be the same as the SH3 encoded event signature " + eventCallSh3);
                    sb.AppendLine("Topic 1: " + logs[0].Topics[1] + " should be 69 hex  0x45, padded");
                    sb.AppendLine("Data " + logs[0].Data + " should be the same as the address padded 32 bytes " + transactionInput.From);
               
                    return sb.ToString();
                }
                else
                {
                    //create another transaction which will raise the event
                    await SendTransaction(client, transactionInput.From, receipt.ContractAddress);
                }

            }

            throw new Exception();
        }

        private static async Task SendTransaction(RpcClient client, string addressFrom,
            string contractAddress)
        {
            var transactionInput = new EthSendTransactionInput();
            var ethSendTransaction = new EthSendTransaction(client);
            var function = new FunctionCallEncoder();
            //Input the function method Sha3Encoded (4 bytes) 
            var sha3Signature = "c6888fa1";
            //Define input parameters
            var inputParameters =  new[] { new Parameter("uint", "a") } ;
            //encode the function call (function + parameter input)
            //using 69 as the input
            var functionCall = function.EncodeRequest(sha3Signature, inputParameters, 69);
            transactionInput.From = addressFrom;
            //the destination address is the contract address
            transactionInput.To = contractAddress;
            //use as data the function call
            transactionInput.Data = functionCall;

            var transactionHashFunction = await ethSendTransaction.SendRequestAsync( transactionInput);
        }

        public Type GetRequestType()
        {
            return typeof(DeployContractExecuteTransactionFilterEventTester);
        }
    }
}