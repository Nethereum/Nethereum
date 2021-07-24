using System.Threading;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting
// ReSharper disable ConsiderUsingConfigureAwait

namespace Nethereum.Contracts.IntegrationTests.Deployment
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ContractConstructorDeploymentAndCall
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ContractConstructorDeploymentAndCall(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDeployAContractWithConstructor()
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

               string public contractName = "Multiplier";
           }
           */

            var contractByteCode =
                "0x6060604052604060405190810160405280600a81526020017f4d756c7469706c6965720000000000000000000000000000000000000000000081526020015060016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061008c57805160ff19168380011785556100bd565b828001600101855582156100bd579182015b828111156100bc57825182600050559160200191906001019061009e565b5b5090506100e891906100ca565b808211156100e457600081815060009055506001016100ca565b5090565b5050604051602080610303833981016040528080519060200190919050505b806000600050819055505b506101e2806101216000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a901461004f57806375d0c0dc14610072578063c6888fa1146100ed5761004d565b005b61005c6004805050610119565b6040518082815260200191505060405180910390f35b61007f6004805050610141565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600f02600301f150905090810190601f1680156100df5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b610103600480803590602001909190505061012b565b6040518082815260200191505060405180910390f35b60006000600050549050610128565b90565b60006000600050548202905061013c565b919050565b60016000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156101da5780601f106101af576101008083540402835291602001916101da565b820191906000526020600020905b8154815290600101906020018083116101bd57829003601f168201915b50505050508156";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""contractName"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";

            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, senderAddress,
                new HexBigInteger(900000), 7);

            Assert.NotNull(transactionHash);

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            Assert.NotNull(receipt.ContractAddress);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

            var multiplierFunction = contract.GetFunction("getMultiplier");

            var multiplier = await multiplierFunction.CallAsync<int>();

            Assert.Equal(7, multiplier);

            var contractNameFunction = contract.GetFunction("contractName");

            var name = await contractNameFunction.CallAsync<string>();

            Assert.Equal("Multiplier", name);
        }


        [Fact]
        public async void ShouldDeployAContractWithConstructorProvidingGasPrice()
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

               string public contractName = "Multiplier";
           }
           */

            var contractByteCode =
                "0x6060604052604060405190810160405280600a81526020017f4d756c7469706c6965720000000000000000000000000000000000000000000081526020015060016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061008c57805160ff19168380011785556100bd565b828001600101855582156100bd579182015b828111156100bc57825182600050559160200191906001019061009e565b5b5090506100e891906100ca565b808211156100e457600081815060009055506001016100ca565b5090565b5050604051602080610303833981016040528080519060200190919050505b806000600050819055505b506101e2806101216000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a901461004f57806375d0c0dc14610072578063c6888fa1146100ed5761004d565b005b61005c6004805050610119565b6040518082815260200191505060405180910390f35b61007f6004805050610141565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600f02600301f150905090810190601f1680156100df5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b610103600480803590602001909190505061012b565b6040518082815260200191505060405180910390f35b60006000600050549050610128565b90565b60006000600050548202905061013c565b919050565b60016000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156101da5780601f106101af576101008083540402835291602001916101da565b820191906000526020600020905b8154815290600101906020018083116101bd57829003601f168201915b50505050508156";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""contractName"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";


            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture
                .GetWeb3(); //deploy the contract, including abi and a paramter of 7. 

            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode, senderAddress,
                new HexBigInteger(900000), new HexBigInteger(1000), new HexBigInteger(0), 7);

            Assert.NotNull(transactionHash);

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            Assert.NotNull(receipt.ContractAddress);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

            var multiplierFunction = contract.GetFunction("getMultiplier");

            var multiplier = await multiplierFunction.CallAsync<int>();

            Assert.Equal(7, multiplier);

            var contractNameFunction = contract.GetFunction("contractName");

            var name = await contractNameFunction.CallAsync<string>();

            Assert.Equal("Multiplier", name);
        }


        [Fact]
        public async void ShouldDeployAContractWithConstructorProvidingFees()
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

               string public contractName = "Multiplier";
           }
           */

            var contractByteCode =
                "0x6060604052604060405190810160405280600a81526020017f4d756c7469706c6965720000000000000000000000000000000000000000000081526020015060016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061008c57805160ff19168380011785556100bd565b828001600101855582156100bd579182015b828111156100bc57825182600050559160200191906001019061009e565b5b5090506100e891906100ca565b808211156100e457600081815060009055506001016100ca565b5090565b5050604051602080610303833981016040528080519060200190919050505b806000600050819055505b506101e2806101216000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a901461004f57806375d0c0dc14610072578063c6888fa1146100ed5761004d565b005b61005c6004805050610119565b6040518082815260200191505060405180910390f35b61007f6004805050610141565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600f02600301f150905090810190601f1680156100df5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b610103600480803590602001909190505061012b565b6040518082815260200191505060405180910390f35b60006000600050549050610128565b90565b60006000600050548202905061013c565b919050565b60016000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156101da5780601f106101af576101008083540402835291602001916101da565b820191906000526020600020905b8154815290600101906020018083116101bd57829003601f168201915b50505050508156";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""contractName"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";


            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture
                .GetWeb3(); //deploy the contract, including abi and a paramter of 7. 

            var feeEstimate = web3.FeeSuggestion.GeTimePreferenceFeeSuggestionStrategy();

            var fees = await feeEstimate.SuggestFeesAsync();

            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(
                abi: abi,
                contractByteCode: contractByteCode,
                from: senderAddress,
                gas: new HexBigInteger(900000),
                maxFeePerGas: fees[0].MaxFeePerGas.Value.ToHexBigInteger(),
                maxPriorityFeePerGas: fees[0].MaxPriorityFeePerGas.Value.ToHexBigInteger(),
                value: new HexBigInteger(1),
                nonce: null,
                7);


            Assert.NotNull(transactionHash);

            var transaction = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
            Assert.True(transaction.From.IsTheSameAddress(senderAddress));
            Assert.Equal(900000, transaction.Gas.Value);
            Assert.Equal(1, transaction.Value.Value);
            Assert.Equal(fees[0].MaxFeePerGas, transaction.MaxFeePerGas);
            Assert.Equal(fees[0].MaxPriorityFeePerGas, transaction.MaxPriorityFeePerGas);


            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            Assert.NotNull(receipt.ContractAddress);


            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

            var multiplierFunction = contract.GetFunction("getMultiplier");

            var multiplier = await multiplierFunction.CallAsync<int>();

            Assert.Equal(7, multiplier);

            var contractNameFunction = contract.GetFunction("contractName");

            var name = await contractNameFunction.CallAsync<string>();

            Assert.Equal("Multiplier", name);
        }

        [Fact]
        public async void ShouldEstimateContactDeployment()
        {
            var contractByteCode =
                "0x6060604052604060405190810160405280600a81526020017f4d756c7469706c6965720000000000000000000000000000000000000000000081526020015060016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061008c57805160ff19168380011785556100bd565b828001600101855582156100bd579182015b828111156100bc57825182600050559160200191906001019061009e565b5b5090506100e891906100ca565b808211156100e457600081815060009055506001016100ca565b5090565b5050604051602080610303833981016040528080519060200190919050505b806000600050819055505b506101e2806101216000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806340490a901461004f57806375d0c0dc14610072578063c6888fa1146100ed5761004d565b005b61005c6004805050610119565b6040518082815260200191505060405180910390f35b61007f6004805050610141565b60405180806020018281038252838181518152602001915080519060200190808383829060006004602084601f0104600f02600301f150905090810190601f1680156100df5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b610103600480803590602001909190505061012b565b6040518082815260200191505060405180910390f35b60006000600050549050610128565b90565b60006000600050548202905061013c565b919050565b60016000508054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156101da5780601f106101af576101008083540402835291602001916101da565b820191906000526020600020905b8154815290600101906020018083116101bd57829003601f168201915b50505050508156";

            var abi =
                @"[{""constant"":true,""inputs"":[],""name"":""getMultiplier"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""contractName"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""}]";

            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var estimate = await web3.Eth.DeployContract.EstimateGasAsync(abi, contractByteCode, senderAddress, 7);

            Assert.NotNull(estimate);
        }
    }
}