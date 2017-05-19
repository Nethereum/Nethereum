using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.Web3.TransactionReceipts;
using Xunit;

namespace Nethereum.Tutorials
{
    public class AccountTransfer
    {
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
            var transactionPolling = new TransactionReceiptPollingService(web3);

            var currentBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);
            //assumed client is mining already

            //When sending the transaction using the transaction manager for a managed account, personal_sendTransaction is used.
            var transactionReceipt = await transactionPolling.SendRequestAsync(() =>
                web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20))
            );

            var newBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);

            Assert.Equal(currentBalance.Value + 20, newBalance.Value);
        }

        [Fact]
        public async Task ShouldBeAbleToTransferBetweenAccountsUsingThePrivateKey()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var addressTo = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var password = "password";

            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";


            // The default account is an account which is mananaged by the user

            var account = new Account(privateKey);
            var web3 = new Web3.Web3(account);

            //The transaction receipt polling service is a simple utility service to poll for receipts until mined
            var transactionPolling = new TransactionReceiptPollingService(web3);

            var currentBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);
            //assumed client is mining already

            //when sending a transaction using an Account, a raw transaction is signed and send using the private key
            var transactionReceipt = await transactionPolling.SendRequestAsync(() =>
                web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20))
            );

            var newBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);

            Assert.Equal(currentBalance.Value + 20, newBalance.Value);
        }


        [Fact]
        public async Task ShouldBeAbleToTransferBetweenAccountsLoadingEncryptedPrivateKey()
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var addressTo = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var password = "password";

            var keyStoreEncryptedJson =
             @"{""crypto"":{""cipher"":""aes-128-ctr"",""ciphertext"":""b4f42e48903879b16239cd5508bc5278e5d3e02307deccbec25b3f5638b85f91"",""cipherparams"":{""iv"":""dc3f37d304047997aa4ef85f044feb45""},""kdf"":""scrypt"",""mac"":""ada930e08702b89c852759bac80533bd71fc4c1ef502291e802232b74bd0081a"",""kdfparams"":{""n"":65536,""r"":1,""p"":8,""dklen"":32,""salt"":""2c39648840b3a59903352b20386f8c41d5146ab88627eaed7c0f2cc8d5d95bd4""}},""id"":""19883438-6d67-4ab8-84b9-76a846ce544b"",""address"":""12890d2cce102216644c59dae5baed380d84830c"",""version"":3}";

            //this is your wallet key  file which can be found on

            //Linux: ~/.ethereum/keystore
            //Mac: /Library/Ethereum/keystore
            //Windows: %APPDATA%/Ethereum


            //if not using portable or netstandard (^net45) you can use LoadFromKeyStoreFile to load the file from the file system.

            var keyStoreService = new KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromJson(password, keyStoreEncryptedJson);

            var account = new Account(key);
            var web3 = new Web3.Web3(account);

            //The transaction receipt polling service is a simple utility service to poll for receipts until mined
            var transactionPolling = new TransactionReceiptPollingService(web3);

            var currentBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);

            //assumed client is mining already
            //when sending a transaction using an Account, a raw transaction is signed and send using the private key
            var transactionReceipt = await transactionPolling.SendRequestAsync(() =>
                web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20))
            );

            var newBalance = await web3.Eth.GetBalance.SendRequestAsync(addressTo);

            Assert.Equal(currentBalance.Value + 20, newBalance.Value);
        }
    }
}