using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3.Sample
{

    public class ContractDeploymentAndCall
    {
        public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode =
                "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = new Web3();

            //deploy the contract, no need to use the abi as we don't have a constructor
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(contractByteCode, addressFrom);

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
            //visual test 
            return "The result of deploying a contract and calling a function to multiply 7 by 69 is: " + result +
                   " and should be 483";
        }
    }
}