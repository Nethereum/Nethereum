using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3.Sample
{
    public class ContractConstructorDeploymentAndCall
    {
        public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            /*
               contract test { 

               uint _multiplier;

               function test(uint multiplier){
                   _multiplier = multiplier;
               }

               function getMultiplier() constant returns(uint d){
                    return _multiplier;
               }

               function multiply(uint a) returns(uint d) { return a * _multiplier; }

           }
           */

            var contractByteCode =
              "0x606060405260405160208060ea833981016040528080519060200190919050505b806000600050819055505b5060b28060386000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a90146041578063c6888fa114606257603f565b005b604c6004805050608c565b6040518082815260200191505060405180910390f35b60766004808035906020019091905050609d565b6040518082815260200191505060405180910390f35b60006000600050549050609a565b90565b60006000600050548202905060ad565b91905056";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = new Web3();

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, addressFrom, new HexBigInteger(900000), 7);

            //the contract should be mining now

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(500);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var result = await multiplyFunction.CallAsync<int>(69);

            var multiplierFunction = contract.GetFunction("getMultiplier");

            var multiplier = await multiplierFunction.CallAsync<int>();

            //visual test 
            return "The result of deploying a contract and calling a function to multiply " + multiplier + " by 69 is: " + result +
                   " and should be 483";
        }
    }
}