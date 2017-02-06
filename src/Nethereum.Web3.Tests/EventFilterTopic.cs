using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3.Tests
{
    public class EventFilterTopic
    {
        public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            /*
            contract test { 
    
                uint _multiplier;
    
                event Multiplied(uint indexed a);
    
                function test(uint multiplier){
                    _multiplier = multiplier;
                }
    
                function multiply(uint a, uint id) returns(uint d) { 
        
                    Multiplied(a);
        
                    return a * _multiplier; 
        
                }
    
            }
           
           */

            var contractByteCode =
                "606060405260405160208060de833981016040528080519060200190919050505b806000600050819055505b5060a68060386000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000817f61aa1562c4ed1a53026a57ad595b672e1b7c648166127b904365b44401821b7960405180905060405180910390a26000600050548202905060a1565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""}],""name"":""Multiplied"",""type"":""event""}]
";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = new Web3();

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash = await web3.Eth.GetDeployContract().SendRequestAsync(abi, contractByteCode, addressFrom, 7);

            
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
            //filter first indexed parameter
            var filter69 = await multipliedEvent.CreateFilterAsync(new object[] {69});
            

            await Task.Delay(2000);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
        
            var transaction69 = await multiplyFunction.SendTransactionAsync(addressFrom, null, 69);
            var transaction18 = await multiplyFunction.SendTransactionAsync(addressFrom, null, 18);
            var transaction7 =  await multiplyFunction.SendTransactionAsync(addressFrom, null, 7);


            TransactionReceipt receiptTransaction = null;

            while (receiptTransaction == null)
            {
                await Task.Delay(5000);
                receiptTransaction = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction7);
            }

            var logsAll = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filterAll);
            var logs69 = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filter69);
            


            return "All logs :" + logsAll.Length + " Logs for 69 " + logs69.Length;

        }
    }
}