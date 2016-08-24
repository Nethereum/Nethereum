using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.Tutorials
{
    public class Deployment
    {
		[Fact]
        public async Task ShouldBeAbleToDeployAContract()
        {
          var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
          var password = "password";
          var abi = @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""r"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
          var byteCode = "0x606060405260405160208060ae833981016040528080519060200190919050505b806000600050819055505b5060768060386000396000f360606040526000357c0100000000000000000000000000000000000000000000000000000000900480631df4f144146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600060005054820290506071565b91905056";
          var multiplier = 7;
          var web3 = new Web3.Web3();
          var unlockResult = await web3.Personal.UnlockAccount.SendRequestAsync(senderAddress, password, new HexBigInteger(60));
          Assert.True(unlockResult);

          var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, multiplier);

          var miningResult = await web3.Miner.Start.SendRequestAsync(6);
          Assert.True(miningResult);
          
          var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

          while(receipt == null){
              Thread.Sleep(1000);
              receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
          }
          var contractAddress = receipt.ContractAddress;

          var contract = web3.Eth.GetContract(abi, contractAddress);

          var multiplyFunction = contract.GetFunction("multiply");

          var result = await multiplyFunction.CallAsync<int>(7);

          Assert.Equal(49, result);
        }

    }
}
