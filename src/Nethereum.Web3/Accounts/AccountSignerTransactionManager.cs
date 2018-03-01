using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Web3.Accounts
{
    public class AccountSignerTransactionManager : TransactionManagerBase
    {
        private readonly TransactionSigner _transactionSigner;
        public override BigInteger DefaultGasPrice { get; set; } = Transaction.DEFAULT_GAS_PRICE;
        public override BigInteger DefaultGas { get; set; } = Transaction.DEFAULT_GAS_LIMIT;
        private Web3 web3;

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public AccountSignerTransactionManager(IClient rpcClient, Account account)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
            Client = rpcClient;
            _transactionSigner = new TransactionSigner();
        }

        public AccountSignerTransactionManager(Web3 web3, Account account) : this(web3?.Client, account)
        {
            this.web3 = web3;
        }

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public AccountSignerTransactionManager(IClient rpcClient, string privateKey)
        {
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            Client = rpcClient;
            Account = new Account(privateKey);
            Account.NonceService = new InMemoryNonceService(Account.Address, rpcClient);
            _transactionSigner = new TransactionSigner();
        }

        public AccountSignerTransactionManager(Web3 web3, string privateKey) : this(web3?.Client, privateKey)
        {
            this.web3 = web3;
        }

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public AccountSignerTransactionManager(string privateKey):this((IClient)null, privateKey)
        {
        }

        public override Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return SignAndSendTransactionAsync(transactionInput);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
            {
                if (Account.NonceService == null)
                {
                    Account.NonceService = new InMemoryNonceService(Account.Address, Client);
                }
                Account.NonceService.Client = Client;
                nonce = await Account.NonceService.GetNextNonceAsync();
            }
            return nonce;
        }

        private async Task<string> SignAndSendTransactionAsync(TransactionInput transaction)
        {
            if(Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.From.EnsureHexPrefix().ToLower() != Account.Address.EnsureHexPrefix().ToLower()) throw new Exception("Invalid account used signing");
            SetDefaultGasPriceAndCostIfNotSet(transaction);

            var ethSendTransaction = new EthSendRawTransaction(Client);
            var nonce = await GetNonceAsync(transaction);

            var gasPrice = transaction.GasPrice;
            var gasLimit = transaction.Gas;
            
            var value = transaction.Value;
            if (value == null)
                value = new HexBigInteger(0);

            var privateKey = new PrivateKey(((Account)Account).PrivateKey);

            string signedTransaction;
            if (web3 != null && web3.ChainId.HasValue)
                signedTransaction = _transactionSigner.SignTransaction(privateKey, transaction.To, value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data, web3.ChainId.Value);
            else
                signedTransaction = _transactionSigner.SignTransaction(privateKey, transaction.To, value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data);

            return await ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }
    }
}