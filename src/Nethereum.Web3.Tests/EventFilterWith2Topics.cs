using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Web3.Sample
{
    public class EventFilterWith2Topics
    {
        [Fact]
        public async Task Test()
        {
            //The compiled solidity contract to be deployed
            /*
          contract test { 
    
                uint _multiplier;
    
                event Multiplied(uint indexed a, uint indexed result);
    
                event MultipliedLog(uint indexed a, uint indexed result, string indexed hello, address sender );
    
                function test(uint multiplier){
                    _multiplier = multiplier;
                }
    
                function multiply(uint a) returns(uint d) {
                    d = a * _multiplier;
                    Multiplied(a, d);
                    MultipliedLog(a, d, "Hello world", msg.sender);
                    return d;
                }
    
                function multiply1(uint a) returns(uint d) {
                    return a * _multiplier;
                }
    
                function multiply2(uint a, uint b) returns(uint d){
                    return a * b;
                }
    
            }
           
           */

            var contractByteCode =
                "0x6060604052604051602080610213833981016040528080519060200190919050505b806000600050819055505b506101d88061003b6000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806361325dbc1461004f578063c23f4e3e1461007b578063c6888fa1146100b05761004d565b005b61006560048080359060200190919050506100dc565b6040518082815260200191505060405180910390f35b61009a60048080359060200190919080359060200190919050506100f2565b6040518082815260200191505060405180910390f35b6100c66004808035906020019091905050610104565b6040518082815260200191505060405180910390f35b6000600060005054820290506100ed565b919050565b600081830290506100fe565b92915050565b600060006000505482029050805080827f51ae5c4fa89d1aa731ff280d425357e6e5c838c6fc8ed6ca0139ea31716bbd5760405180905060405180910390a360405180807f48656c6c6f20776f726c64000000000000000000000000000000000000000000815260200150600b019050604051809103902081837f74053123e4f45ba0f8cbf86301034a4ab00cdc75cd155a0df7c5d815bd97dcb533604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a48090506101d3565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply1"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""},{""name"":""b"",""type"":""uint256""}],""name"":""multiply2"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""}],""name"":""Multiplied"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""},{""indexed"":true,""name"":""sender"",""type"":""string""},{""indexed"":false,""name"":""hello"",""type"":""address""}],""name"":""MultipliedLog"",""type"":""event""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";

            var web3 = new Web3(ClientFactory.GetClient());

            var result = await web3.Personal.UnlockAccount.SendRequestAsync(addressFrom, pass, new HexBigInteger(600000));
            Assert.True(result, "Account should be unlocked");

           

            var eth = web3.Eth;
            var transactions = eth.Transactions;

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash = await eth.DeployContract.SendRequestAsync(abi, contractByteCode, addressFrom, new HexBigInteger(900000), 7);

            Assert.NotNull(transactionHash);

            result = await web3.Miner.Start.SendRequestAsync(4);
            Assert.True(result, "Mining should have started");

            //the contract should be mining now

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(5000);
                receipt = await transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            var code = await web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

            if (String.IsNullOrEmpty(code))
            {
                throw new Exception("Code was not deployed correctly, verify bytecode or enough gas was uto deploy the contract");
            }
       
   
            var contract = eth.GetContract(abi, receipt.ContractAddress);

            var multipliedEvent = contract.GetEvent("Multiplied");
            var filterAllContract = await contract.CreateFilterAsync();
            var filterAll = await multipliedEvent.CreateFilterAsync();
            //filter on the first indexed parameter
            var filter69 = await multipliedEvent.CreateFilterAsync(69);
            //filter on the second indexed parameter
            var filter49 = await multipliedEvent.CreateFilterAsync<object, int>(null, 49);
            //filter OR on the first indexed parameter
            var filter69And18 = await multipliedEvent.CreateFilterAsync(new[] { 69, 18 });


            var multipliedEventLog =  contract.GetEvent("MultipliedLog");
            var filterAllLog = await multipliedEventLog.CreateFilterAsync();

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
           
            var gas = await multiplyFunction.EstimateGasAsync(69);
            var transaction69 = await multiplyFunction.SendTransactionAsync(addressFrom, 69);
            var transaction18 = await multiplyFunction.SendTransactionAsync(addressFrom, 18);
            var transaction7 = await multiplyFunction.SendTransactionAsync(addressFrom, 7);

            var multiplyFunction2 = contract.GetFunction("multiply2");
            var callResult = await multiplyFunction2.CallAsync<int>(7, 7);

            TransactionReceipt receiptTransaction = null;

            while (receiptTransaction == null)
            {
                await Task.Delay(5000);
                receiptTransaction = await transactions.GetTransactionReceipt.SendRequestAsync(transaction7);
            }

            result = await web3.Miner.Stop.SendRequestAsync();
            Assert.True(result, "Mining should have stopped");

            var logs = await eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filterAllContract);    
            var eventLogsAll = await multipliedEvent.GetFilterChanges<EventMultiplied>(filterAll);
            var eventLogs69 = await multipliedEvent.GetFilterChanges<EventMultiplied>(filter69);
            var eventLogsResult49 = await multipliedEvent.GetFilterChanges<EventMultiplied>(filter49);
            var eventLogsFor69and18 = await multipliedEvent.GetFilterChanges<EventMultiplied>(filter69And18);

            
            var multipliedLogEvents = await multipliedEventLog.GetFilterChanges<EventMultipliedSenderLog>(filterAllLog);

            Assert.Equal(eventLogs69.First().Event.Result, 483);
            Assert.Equal(multipliedLogEvents.First().Event.Hello, "0xed6c11b0b5b808960df26f5bfc471d04c1995b0ffd2055925ad1be28d6baadfd"); //The sha3 keccak of "Hello world" as it is an indexed string
            Assert.Equal(multipliedLogEvents.First().Event.Sender, addressFrom);

       
        } 

        public class EventMultiplied
        {
            [Parameter("uint", "a", 1, true)]
            public int A { get; set; }

            [Parameter("uint", "result", 2, true)]
            public int Result { get; set; }
        }

        public class EventMultipliedSenderLog
        {
            [Parameter("uint", "a", 1, true)]
            public int A { get; set; }

            [Parameter("uint", "result", 2, true)]
            public int Result { get; set; }

            [Parameter("address", "sender", 4, false)]
            public string Sender { get; set; }

            
            [Parameter("string", "hello", 3, true)]
            public string Hello { get; set; }

        }
    }
}