using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3.Sample
{
    public class EventFilterWith2Topics
    {
        public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            /*
           contract test { 
    
                uint _multiplier;
    
                event Multiplied(uint indexed a, uint indexed result);
    
                function test(uint multiplier){
                    _multiplier = multiplier;
                }
    
                function multiply(uint a) returns(uint d) { 
                    d = a * _multiplier; 
                    Multiplied(a, d);
        
                }
    
            }
           
           */

            var contractByteCode =
                "606060405260405160208060de833981016040528080519060200190919050505b806000600050819055505b5060a68060386000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b600060006000505482029050805080827f51ae5c4fa89d1aa731ff280d425357e6e5c838c6fc8ed6ca0139ea31716bbd5760405180905060405180910390a35b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""}],""name"":""Multiplied"",""type"":""event""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = new Web3();

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, addressFrom, 7);

            //the contract should be mining now

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(5000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            var multipliedEvent = contract.GetEvent("Multiplied");
            var filterAll = await multipliedEvent.CreateFilterAsync();
            //filter on the first indexed parameter
            var filter69 = await multipliedEvent.CreateFilterAsync(new object[] { 69 });
            //filter on the second indexed parameter
            var filter49 = await multipliedEvent.CreateFilterAsync(null, new object[] { 49 });
            //filter OR on the first indexed parameter
            var filter69And18 = await multipliedEvent.CreateFilterAsync(new object[] { 69, 18 });

            

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
          
            var transaction69 = await multiplyFunction.SendTransactionAsync(addressFrom, null, 69);
            var transaction18 = await multiplyFunction.SendTransactionAsync(addressFrom, null, 18);
            var transaction7 = await multiplyFunction.SendTransactionAsync(addressFrom, null, 7);


            TransactionReceipt receiptTransaction = null;

            while (receiptTransaction == null)
            {
                await Task.Delay(5000);
                receiptTransaction = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction7);
            }

            var logsAll = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filterAll);
            var logsAllDecoded = DecodeAllEvents<EventMultiplied>(logsAll);
            var logs69 = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filter69);
            var logs69Decoded = DecodeAllEvents<EventMultiplied>(logs69);
            var logs49result = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filter49);
            var logs49Decoded = DecodeAllEvents<EventMultiplied>(logs49result);
            var logs69And18 = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filter69And18);
            var logs69And18Decoded = DecodeAllEvents<EventMultiplied>(logs69And18);

            return "All logs :" + logsAll.Length + " Logs for 69 " + logs69.Length;

        }

        public List<T> DecodeAllEvents<T>(NewFilterLog[] logs) where T : new()
        {
            var result = new List<T>();
            var eventDecoder = new EventTopicDecoder();
            foreach (var log in logs)
            {
                result.Add(eventDecoder.DecodeTopics<T>(log.Topics, log.Data));   
            }
            return result;
        }

        public class EventMultiplied
        {
            [Parameter("uint", "a", 1, true)]
            public int A { get; set; }

            [Parameter("uint", "result", 2, true)]
            public int Result { get; set; }
        }
    }
}