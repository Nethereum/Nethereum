using Nethereum.Web3.Accounts;
using Nethereum.KeyStore;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System;
using System.Numerics;
using Org.BouncyCastle.Security;

namespace Nethereum.Mixer.Console
{
    public class SimpleMixerService
    {

        public Account CreateAccount(string password, string path)
        {
            //Generate a private key pair using SecureRandom
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            //Get the public address (derivied from the public key)
            var address = ecKey.GetPublicAddress();

            //Create a store service, to encrypt and save the file using the web3 standard
            var service = new KeyStoreService();
            var encryptedKey = service.EncryptAndGenerateDefaultKeyStoreAsJson(password, ecKey.GetPrivateKeyAsBytes(), address);
            var fileName = service.GenerateUTCFileName(address);
            //save the File
            using (var newfile = File.CreateText(Path.Combine(path, fileName)))
            {
                newfile.Write(encryptedKey);
                newfile.Flush();
            }

            return new Account(ecKey.GetPrivateKey());
        }

        public Account LoadFromKeyStoreFile(string filePath, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            using (var file = File.OpenText(filePath))
            {
                var json = file.ReadToEnd();
                var key = keyStoreService.DecryptKeyStoreFromJson(password, json);
                return new Account(key);
            }
        }

        public async Task CreateAccountsAndMixBalances(string initialWalletPath, string password, string newAccountsPath, int numberOfAccounts, string rpcPath)
        {
            //new accounts
            var accounts = new List<Account>();

            if (!Directory.Exists(newAccountsPath))
            {
                Directory.CreateDirectory(newAccountsPath);
            }

            for (var i = 0; i < numberOfAccounts; i++)
            {
                accounts.Add(CreateAccount(password, newAccountsPath));
            }

            foreach (var file in Directory.GetFiles(initialWalletPath))
            {
                var account = LoadFromKeyStoreFile(file, password);
                var web3 = new Web3.Web3(account, rpcPath);
                var accountBalance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
                var transactionReceiptsInitial = await TransferRandomAmounts(web3, account, accountBalance, accounts.ToArray());

            }

        }

        public async Task<List<string>> TransferRandomAmounts(Web3.Web3 web3, Account fromAccount, BigInteger amount,
            params Account[] accounts)
        {
            var transfers = new List<string>();

            var ethAmount = Web3.Web3.Convert.FromWei(amount);
            if (Decimal.Truncate(ethAmount) > 0)
            {
                var secureRandom = new SecureRandom();
                BigInteger totalAmountDistributed = 0;
                string lastTransaction = null;


                for (int i = 0; i < accounts.Length; i++)
                {

                    BigInteger currentAmount = 0;
                    if (i == accounts.Length - 1)
                    {
                        if (lastTransaction != null)
                        {
                            var receipt = await PollForReceiptAsync(web3, lastTransaction, 15000);
                        }
                        currentAmount = await web3.Eth.GetBalance.SendRequestAsync(fromAccount.Address);
                        currentAmount = currentAmount - (web3.TransactionManager.DefaultGas * web3.TransactionManager.DefaultGasPrice);
                    }
                    else
                    {
                        currentAmount = 0;
                        while (currentAmount == 0)
                        {
                            var percentage = secureRandom.Next(100 / accounts.Length - 1);
                            var percentageAmount = Decimal.Truncate(ethAmount * percentage / 100);

                            if (percentageAmount > 0)
                            {
                                currentAmount = Web3.Web3.Convert.ToWei(percentageAmount);
                            }
                        }
                        totalAmountDistributed = totalAmountDistributed + currentAmount;
                    }

                    var txn = await web3.Eth.TransactionManager.SendTransactionAsync(fromAccount.Address, accounts[i].Address, new HexBigInteger(currentAmount));
                    transfers.Add(txn);
                    lastTransaction = txn;

                    System.Console.WriteLine("Txn: " + txn + " Transferred to:" + accounts[i].Address + " Total: " + Web3.Web3.Convert.FromWei(currentAmount).ToString());

                }
            }
            return transfers;
        }

        public async Task<TransactionReceipt> PollForReceiptAsync(Web3.Web3 web3, string transaction, int retryMilliseconds)
        {
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            while (receipt == null)
            {
                await Task.Delay(retryMilliseconds);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction).ConfigureAwait(false);
            }
            return receipt;
        }
    }

}