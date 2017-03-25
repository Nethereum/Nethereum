using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Xunit;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Transactions;

namespace Nethereum.Tutorials
{
    public class Deployment
    { 
        [Fact]
        public async Task ShouldBeAbleToDeployAContractUsingPersonalUnlock()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var password = "password";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = new Web3.Web3(new ManagedAccount(senderAddress, password));
            
            var transactionPolling = new TransactionReceiptPollingService(web3);

            //assumed client is mining already
            var contractAddress = await 
                transactionPolling.DeployContractAndGetAddressAsync(
                        () => web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), multiplier)
                    );

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);

        }

        [Fact]
        public async Task ShouldBeAbleToDeployAContractUsingPrivateKey()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = new Web3.Web3(new Account(privateKey));

            var transactionPolling = new TransactionReceiptPollingService(web3);

            //assumed client is mining already
            var contractAddress = await
                transactionPolling.DeployContractAndGetAddressAsync(
                        () => web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), multiplier)
                    );

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);

        }

        [Fact]
        public async Task ShouldBeAbleToDeployAContractLoadingEncryptedPrivateKey()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var password = "password";
            //this is your wallet key file which can be found on

            //Linux: ~/.ethereum/keystore
            //Mac: /Library/Ethereum/keystore
            //Windows: %APPDATA%/Ethereum

            var keyStoreEncryptedJson = @"{""crypto"":{""cipher"":""aes-128-ctr"",""ciphertext"":""b4f42e48903879b16239cd5508bc5278e5d3e02307deccbec25b3f5638b85f91"",""cipherparams"":{""iv"":""dc3f37d304047997aa4ef85f044feb45""},""kdf"":""scrypt"",""mac"":""ada930e08702b89c852759bac80533bd71fc4c1ef502291e802232b74bd0081a"",""kdfparams"":{""n"":65536,""r"":1,""p"":8,""dklen"":32,""salt"":""2c39648840b3a59903352b20386f8c41d5146ab88627eaed7c0f2cc8d5d95bd4""}},""id"":""19883438-6d67-4ab8-84b9-76a846ce544b"",""address"":""12890d2cce102216644c59dae5baed380d84830c"",""version"":3}";

            var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            //if not using portable or netstandard (^net45) you can use LoadFromKeyStoreFile to load the file from the file system.

            var keyStoreService = new KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromJson(password, keyStoreEncryptedJson);
           

            var web3 = new Web3.Web3(new Account(key));

            var transactionPolling = new TransactionReceiptPollingService(web3);

            //assumed client is mining already
            var contractAddress = await
                transactionPolling.DeployContractAndGetAddressAsync(
                        () => web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000), multiplier)
                    );

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);
        }
    }
}
