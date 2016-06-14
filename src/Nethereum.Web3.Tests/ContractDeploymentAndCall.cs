using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Web3.Sample
{

    public class ContractDeploymentAndCall
    {
        [Fact]
        public async void ShouldDeployAContractAndPerformACall()
        {
            //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode =
                "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";

            var web3 = new Web3(ClientFactory.GetClient());

            var result = await web3.Personal.UnlockAccount.SendRequestAsync(addressFrom, pass, new HexBigInteger(600));
            Assert.True(result, "Account should be unlocked");


            //deploy the contract, no need to use the abi as we don't have a constructor
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(contractByteCode, addressFrom);
            Assert.NotNull(transactionHash);
            //the contract should be mining now

            result = await web3.Personal.LockAccount.SendRequestAsync(addressFrom);
            Assert.True(result, "Account should be locked");

            result = await web3.Miner.Start.SendRequestAsync();
            Assert.True(result, "Mining should have started");
            //the contract should be mining now

            //get the contract address 
            TransactionReceipt receipt = null;
            
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            Assert.NotNull(receipt.ContractAddress);

            result = await web3.Miner.Stop.SendRequestAsync();
            Assert.True(result, "Mining should have stopped");

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

        }
    }
}