using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.Web3.Tests
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

            var web3 = new Web3(ClientFactory.GetClient());
            var gethTester = GethTesterFactory.GetLocal(web3);

            var receipt = await gethTester.DeployTestContractLocal(contractByteCode);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
            
            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);

        }

        [Fact]
        public async void ShouldDeployUsingMultipleParameters()
        {
            var contractByteCode =
              "0x606060408181528060bd833960a090525160805160009182556001556095908190602890396000f3606060405260e060020a60003504631df4f1448114601c575b6002565b34600257608360043560015460005460408051918402909202808252915173ffffffffffffffffffffffffffffffffffffffff33169184917f841774c8b4d8511a3974d7040b5bc3c603d304c926ad25d168dacd04e25c4bed9181900360200190a3919050565b60408051918252519081900360200190f3";

            var abi =
                @"[{'constant':false,'inputs':[{'name':'a','type':'int256'}],'name':'multiply','outputs':[{'name':'r','type':'int256'}],'payable':false,'type':'function'},{'inputs':[{'name':'multiplier','type':'int256'},{'name':'another','type':'int256'}],'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'a','type':'int256'},{'indexed':true,'name':'sender','type':'address'},{'indexed':false,'name':'result','type':'int256'}],'name':'Multiplied','type':'event'}]";

            var web3 = new Web3(ClientFactory.GetClient());

            var gethTester = GethTesterFactory.GetLocal(web3);

            await web3.Miner.Start.SendRequestAsync(6);
            var transaction =
                await
                    web3.Eth.DeployContract.SignAndSendRequestAsync(gethTester.Password, abi, contractByteCode,
                        gethTester.Account, new HexBigInteger(900000), 7, 8);

            var receipt = await gethTester.GetTransactionReceipt(transaction);

            await web3.Miner.Stop.SendRequestAsync();

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(3864, callResult);
        }

    }
}
