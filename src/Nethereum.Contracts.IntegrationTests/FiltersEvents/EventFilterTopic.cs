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

        private readonly string _privateKey = "0x00b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

        [Fact]
        public async Task StoreAndRetrieveStructs()
        {

            var abi =
                @"[{'constant':true,'inputs':[{'name':'','type':'bytes32'},{'name':'','type':'uint256'}],'name':'documents','outputs':[{'name':'name','type':'string'},{'name':'description','type':'string'},{'name':'sender','type':'address'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'key','type':'bytes32'},{'name':'name','type':'string'},{'name':'description','type':'string'}],'name':'storeDocument','outputs':[{'name':'success','type':'bool'}],'payable':false,'stateMutability':'nonpayable','type':'function'}]";

            var smartContractByteCode =
                "6060604052341561000f57600080fd5b6105408061001e6000396000f30060606040526004361061004b5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166379c17cc581146100505780638553139c14610189575b600080fd5b341561005b57600080fd5b610069600435602435610235565b60405173ffffffffffffffffffffffffffffffffffffffff821660408201526060808252845460026000196101006001841615020190911604908201819052819060208201906080830190879080156101035780601f106100d857610100808354040283529160200191610103565b820191906000526020600020905b8154815290600101906020018083116100e657829003601f168201915b50508381038252855460026000196101006001841615020190911604808252602090910190869080156101775780601f1061014c57610100808354040283529160200191610177565b820191906000526020600020905b81548152906001019060200180831161015a57829003601f168201915b50509550505050505060405180910390f35b341561019457600080fd5b610221600480359060446024803590810190830135806020601f8201819004810201604051908101604052818152929190602084018383808284378201915050505050509190803590602001908201803590602001908080601f01602080910402602001604051908101604052818152929190602084018383808284375094965061028795505050505050565b604051901515815260200160405180910390f35b60006020528160005260406000208181548110151561025057fe5b60009182526020909120600390910201600281015490925060018301915073ffffffffffffffffffffffffffffffffffffffff1683565b6000610291610371565b60606040519081016040908152858252602080830186905273ffffffffffffffffffffffffffffffffffffffff33168284015260008881529081905220805491925090600181016102e2838261039f565b600092835260209092208391600302018151819080516103069291602001906103d0565b506020820151816001019080516103219291602001906103d0565b506040820151600291909101805473ffffffffffffffffffffffffffffffffffffffff191673ffffffffffffffffffffffffffffffffffffffff9092169190911790555060019695505050505050565b60606040519081016040528061038561044e565b815260200161039261044e565b8152600060209091015290565b8154818355818115116103cb576003028160030283600052602060002091820191016103cb9190610460565b505050565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061041157805160ff191683800117855561043e565b8280016001018555821561043e579182015b8281111561043e578251825591602001919060010190610423565b5061044a9291506104b3565b5090565b60206040519081016040526000815290565b6104b091905b8082111561044a57600061047a82826104cd565b6104886001830160006104cd565b5060028101805473ffffffffffffffffffffffffffffffffffffffff19169055600301610466565b90565b6104b091905b8082111561044a57600081556001016104b9565b50805460018160011615610100020316600290046000825580601f106104f35750610511565b601f01602090049060005260206000209081019061051191906104b3565b505600a165627a7a72305820049f1f3ad86cf097dd9c5de014d2e718b5b6b9a05b091d4daebcf60dd3e1213c0029";

            var account = new Account(_privateKey);
            var web3 = new Web3.Web3(account);

            var accountBalance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);

            Assert.True(accountBalance.Value > 0);

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    abi,
                    smartContractByteCode,
                    account.Address,
                    new HexBigInteger(900000));

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);
            var storeDocumentFunction = contract.GetFunction("storeDocument");

            var receipt1 = await storeDocumentFunction.SendTransactionAndWaitForReceiptAsync(account.Address, new HexBigInteger(900000), null, null, "k1", "doc1", "Document 1");
            Assert.Equal(1, receipt1.Status?.Value);
            var receipt2 = await storeDocumentFunction.SendTransactionAndWaitForReceiptAsync(account.Address, new HexBigInteger(900000), null, null, "k2", "doc2", "Document 2");
            Assert.Equal(1, receipt2.Status?.Value);

            var documentsFunction = contract.GetFunction("documents");
            var document1 = await documentsFunction.CallDeserializingToObjectAsync<Document>("k1", 0);
            var document2 = await documentsFunction.CallDeserializingToObjectAsync<Document>("k2", 0);

            Assert.Equal("doc1", document1.Name);
            Assert.Equal("doc2", document2.Name);

        }

        [FunctionOutput]
        public class Document
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("string", "description", 2)]
            public string Description { get; set; }

            [Parameter("address", "sender", 3)]
            public string Sender { get; set; }

        }
      
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