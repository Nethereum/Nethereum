using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Accounts.IntegrationTests
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class AccountTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public AccountTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
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

            var keyStoreEncryptedJson =
                @"{""crypto"":{""cipher"":""aes-128-ctr"",""ciphertext"":""b4f42e48903879b16239cd5508bc5278e5d3e02307deccbec25b3f5638b85f91"",""cipherparams"":{""iv"":""dc3f37d304047997aa4ef85f044feb45""},""kdf"":""scrypt"",""mac"":""ada930e08702b89c852759bac80533bd71fc4c1ef502291e802232b74bd0081a"",""kdfparams"":{""n"":65536,""r"":1,""p"":8,""dklen"":32,""salt"":""2c39648840b3a59903352b20386f8c41d5146ab88627eaed7c0f2cc8d5d95bd4""}},""id"":""19883438-6d67-4ab8-84b9-76a846ce544b"",""address"":""12890d2cce102216644c59dae5baed380d84830c"",""version"":3}";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            //if not using portable or netstandard (^net45) you can use LoadFromKeyStoreFile to load the file from the file system.

            var acccount = Account.LoadFromKeyStore(keyStoreEncryptedJson, password);

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt = await
                web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000), null, multiplier);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);
        }

        [Fact]
        public async Task ShouldBeAbleToDeployAContractUsingPersonalUnlock()
        {
            
            var senderAddress = AccountFactory.Address;
            var password = AccountFactory.Password;
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = Web3Factory.GetWeb3Managed();

            var receipt = await
                web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000), null, multiplier);

            var contractAddress = receipt.ContractAddress;

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
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt = await
                web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000), null, multiplier);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var multiplyFunction = contract.GetFunction("multiply");

            var result = await multiplyFunction.CallAsync<int>(7);

            Assert.Equal(49, result);
        }

        [Fact]
        public async Task ShouldBeAbleToTransferBetweenAccountsUsingManagedAccount()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var addressTo = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var password = "password";
            // A managed account is an account which is maintained by the client (Geth / Parity)
            var account = new ManagedAccount(senderAddress, password);
            var web3 = new Web3.Web3(account);

            //The transaction receipt polling service is a simple utility service to poll for receipts until mined
            var transactionPolling = (TransactionReceiptPollingService)web3.TransactionManager.TransactionReceiptService;

            var currentBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);
            //assumed client is mining already

            //When sending the transaction using the transaction manager for a managed account, personal_sendTransaction is used.

            var txnHash =
                await web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20));
            var receipt = await transactionPolling.PollForReceiptAsync(txnHash);         

            var newBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);

            Assert.Equal(currentBalance.Value + 20, newBalance.Value);
        }

    }
}