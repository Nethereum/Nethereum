using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    public class EventFilterTopic
    {

        [Fact]
        public async Task DeployAndCallContract_WithEvents()
        {
                   var abi =
                @"[{'constant':false,'inputs':[{'name':'val','type':'int256'}],'name':'multiply','outputs':[{'name':'','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[{'name':'multiplier','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'from','type':'address'},{'indexed':false,'name':'val','type':'int256'},{'indexed':false,'name':'result','type':'int256'}],'name':'Multiplied','type':'event'}]";

            var smartContractByteCode =
                "6060604052341561000f57600080fd5b604051602080610149833981016040528080516000555050610113806100366000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416631df4f14481146043575b600080fd5b3415604d57600080fd5b60566004356068565b60405190815260200160405180910390f35b6000805482027fd01bc414178a5d1578a8b9611adebfeda577e53e89287df879d5ab2c29dfa56a338483604051808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001838152602001828152602001935050505060405180910390a1929150505600a165627a7a723058201bd2fbd3fb58686ed61df3e636dc4cc7c95b864aa1654bc02b0136e6eca9e9ef0029";

            var account = AccountFactory.GetAccount();
            var web3 = Web3Factory.GetWeb3();

            var multiplier = 2;

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    abi,
                    smartContractByteCode,
                    account.Address,
                    new HexBigInteger(900000),
                    null,
                    multiplier);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);
            var multiplyFunction = contract.GetFunction("multiply");

            var multipliedEvent = contract.GetEvent("Multiplied");
            var filterForAll = await multipliedEvent.CreateFilterAsync();

            var estimatedGas = await multiplyFunction.EstimateGasAsync(7);

            var receipt1 = await multiplyFunction.SendTransactionAndWaitForReceiptAsync(account.Address, new HexBigInteger(estimatedGas.Value), null, null, 5);
            var receipt2 = await multiplyFunction.SendTransactionAndWaitForReceiptAsync(account.Address, new HexBigInteger(estimatedGas.Value), null, null, 7);

            Assert.Equal(1, receipt1.Status.Value);
            Assert.Equal(1, receipt2.Status.Value);

            Assert.False(receipt1.HasErrors());
            Assert.False(receipt2.HasErrors());

            var logsForAll = await multipliedEvent.GetFilterChanges<MultipliedEvent>(filterForAll);

            Assert.Equal(2, logsForAll.Count());

        }

        public class MultipliedEvent
        {
            [Parameter("address", "from", 1)]
            public string Sender { get; set; }

            [Parameter("int", "val", 2)]
            public int InputValue { get; set; }

            [Parameter("int", "result", 3)]
            public int Result { get; set; }
        }

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

            var web3 = Web3Factory.GetWeb3();
            var addressFrom = AccountFactory.Address;

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
            //filter first indexed parameter
            var filter69 = await multipliedEvent.CreateFilterAsync(new object[] {69});


            await Task.Delay(2000);

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
            var logs69 = await web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filter69);


            return "All logs :" + logsAll.Length + " Logs for 69 " + logs69.Length;
        }
    }
}