using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Xunit;
using Nethereum.Web3.Accounts.Managed;

namespace Nethereum.Tutorials
{
    public class Deployment
    {
		[Fact]
        public async Task ShouldBeAbleToDeployAContract()
        {
          var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
          var password = "password";
            var abi = @"[{'constant':false,'inputs':[{'name':'val','type':'int256'}],'name':'multiply','outputs':[{'name':'d','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[{'name':'multiplier','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'}]";
            var byteCode =
                "0x6060604052341561000f57600080fd5b6040516020806100f283398101604052808051906020019091905050806000819055505060b1806100416000396000f300606060405260043610603f576000357c0100000000000000000000000000000000000000000000000000000000900463ffffffff1680631df4f144146044575b600080fd5b3415604e57600080fd5b606260048080359060200190919050506078565b6040518082815260200191505060405180910390f35b60008054820290509190505600a165627a7a723058200dfd1138ee1b70e240253d56f9253b3c82bc9f1058e3cb18c2cf7a86691d60b60029";
          var multiplier = 7;
          //a managed account uses personal_sendTransanction with the given password, this way we don't need to unlock the account for a certain period of time
          var account = new ManagedAccount(senderAddress, password);

          //using the specific geth web3 library to allow us manage the mining.
          var web3 = new Geth.Web3Geth(account);

          var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(3000000), null, multiplier);

            //assumed we are mining already, no need to manage it using Nethereum
            // start mining
            // await web3.Miner.Start.SendRequestAsync(6);


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
