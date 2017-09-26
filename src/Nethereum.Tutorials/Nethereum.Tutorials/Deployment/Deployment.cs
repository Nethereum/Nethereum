using Nethereum.Web3.Accounts.Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Tutorials
{
    public class Deployment
    {
		//[Fact(Skip="Unskip it if want to demo")]
        [Fact]
        public async Task ShouldBeAbleToDeployAContract()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var password = "password";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;
            
            //a managed account uses personal_sendTransanction with the given password, this way we don't need to unlock the account for a certain period of time
            var account = new ManagedAccount(senderAddress, password);

            //using the specific geth web3 library to allow us manage the mining.
            var web3 = new Geth.Web3Geth(account);

            // start mining
            await web3.Miner.Start.SendRequestAsync(6);

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress, new Hex.HexTypes.HexBigInteger(900000), null, multiplier);

            var mineResult = await web3.Miner.Stop.SendRequestAsync();
            Assert.True(mineResult);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);

        }

    }
}
