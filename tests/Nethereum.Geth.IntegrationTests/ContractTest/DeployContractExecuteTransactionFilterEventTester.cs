using System;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Geth.RPC.Miner;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Personal;
using Nethereum.RPC.Tests.Testers;
using Nethereum.RPC.Web3;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.IntegrationTests.ContractTest
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting
{
    public class DeployContractExecuteTransactionFilterEventTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
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
            var contractByteCode =
                "606060405260c08060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000817f10f82b5dc139f3677a16d7bfb70c65252e78143313768d2c52e07db775e1c7ab33604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a260078202905060bb565b91905056";
            var minerStart = new MinerStart(client);
            var minerStartResult = await minerStart.SendRequestAsync().ConfigureAwait(false);

            //Create a new Eth Send Transanction RPC Handler

            //Create the transaction input for the new contract

            //On transaction input the compiled contract is the Data, together with our sender address 
            var transactionInput = new TransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x12890D2cce102216644c59daE5baed380d84830c";
            // retrieve the transaction hash, as we need to get a transaction sreceipt with the contract address
            var transactionHash =
                await new PersonalSignAndSendTransaction(client).SendRequestAsync(transactionInput, "password").ConfigureAwait(false);

            //the contract should be mining now

            //Get the transaction receipt using the transactionHash
            var ethGetTransactionReceipt = new EthGetTransactionReceipt(client);
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
                receipt = await ethGetTransactionReceipt.SendRequestAsync(transactionHash).ConfigureAwait(false);

            //sha3 the event call, we can use this to validate our topics 

            var eventCallSh3 =
                await new Web3Sha3(client).SendRequestAsync(new HexUTF8String("Multiplied(uint256,address)")).ConfigureAwait(false);
            //create a filter 
            //just listen to anything no more filter topics (ie int indexed number)
            var ethFilterInput = new NewFilterInput();
            ethFilterInput.FromBlock.SetValue(receipt.BlockNumber);
            ethFilterInput.ToBlock = BlockParameter.CreateLatest();
            ethFilterInput.Address = new[] {receipt.ContractAddress};
            //no topics
            //ethFilterInput.Topics = new object[]{};

            var newEthFilter = new EthNewFilter(client);
            var filterId = await newEthFilter.SendRequestAsync(ethFilterInput).ConfigureAwait(false);


            //create a transaction which will raise the event
            await SendTransaction(client, transactionInput.From, receipt.ContractAddress, "password").ConfigureAwait(false);

            //get filter changes
            var ethGetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter(client);
            FilterLog[] logs = null;

            while (logs == null || logs.Length < 1)
            {
                //Get the filter changes logs
                logs = await ethGetFilterChangesForEthNewFilter.SendRequestAsync(filterId).ConfigureAwait(false);

                if (logs.Length > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Topic 0: " + logs[0].Topics[0] +
                                  " should be the same as the SH3 encoded event signature " + eventCallSh3);
                    Assert.Equal(logs[0].Topics[0], eventCallSh3);
                    sb.AppendLine("Topic 1: " + logs[0].Topics[1] + " should be 69 hex  0x45, padded");

                    sb.AppendLine("Data " + logs[0].Data + " should be the same as the address padded 32 bytes " +
                                  transactionInput.From);

                    return sb.ToString();
                }
            }

            var minerStop = new MinerStop(client);
            var minerStopResult = await minerStop.SendRequestAsync().ConfigureAwait(false);
            throw new Exception("Execution failed");
        }

        public override Type GetRequestType()
        {
            return typeof(DeployContractExecuteTransactionFilterEventTester);
        }

        private static async Task<string> SendTransaction(IClient client, string addressFrom,
            string contractAddress, string password)
        {
            var transactionInput = new TransactionInput();
            var ethSendTransaction = new PersonalSignAndSendTransaction(client);
            var function = new FunctionCallEncoder();
            //Input the function method Sha3Encoded (4 bytes) 
            var sha3Signature = "c6888fa1";
            //Define input parameters
            var inputParameters = new[] {new Parameter("uint", "a")};
            //encode the function call (function + parameter input)
            //using 69 as the input
            var functionCall = function.EncodeRequest(sha3Signature, inputParameters, 69);
            transactionInput.From = addressFrom;
            //the destination address is the contract address
            transactionInput.To = contractAddress;
            //use as data the function call
            transactionInput.Data = functionCall;

            return await ethSendTransaction.SendRequestAsync(transactionInput, password).ConfigureAwait(false);
        }

        [Fact]
        public async void ShouldDeployContractAndPerformCall()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}