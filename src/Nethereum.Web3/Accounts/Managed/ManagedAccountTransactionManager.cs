using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Nethereum.RPC.TransactionManagers;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.NonceServices;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Web3.Accounts.Managed
{
    public class ManagedAccountTransactionManager : TransactionManagerBase
    { 
        public override BigInteger DefaultGasPrice { get; set; } = Transaction.DEFAULT_GAS_PRICE;
        public override BigInteger DefaultGas { get; set; } = Transaction.DEFAULT_GAS_LIMIT;

        public ManagedAccount Account { get; }

        public ManagedAccountTransactionManager(IClient client, ManagedAccount account)
        {
            Account = account;
            Client = client;
        }

        public ManagedAccountTransactionManager(IClient client, string accountAddress, string password)
        {
            Account = new ManagedAccount(accountAddress, password);
            Client = client;
        }

        public ManagedAccountTransactionManager(string accountAddress, string password):this(null, accountAddress, password)
        {
 
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
            {
                if (Account.NonceService != null)
                {
                    Account.NonceService.Client = Client;
                    nonce = await Account.NonceService.GetNextNonceAsync();
                }
            }
            return nonce;
        }


        public override async Task<string> SendTransactionAsync<T>(T transactionInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (transactionInput.From != Account.Address) throw new Exception("Invalid account used");
            SetDefaultGasPriceAndCostIfNotSet(transactionInput);
            var nonce = await GetNonceAsync(transactionInput).ConfigureAwait(false);
            if (nonce != null) transactionInput.Nonce = nonce;
            var ethSendTransaction = new PersonalSignAndSendTransaction(Client);
            return await ethSendTransaction.SendRequestAsync(transactionInput, Account.Password).ConfigureAwait(false);
        }

        public override async Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            if (from != Account.Address) throw new Exception("Invalid account used");
            var transactionInput = new TransactionInput(from, to, amount);
            return await SendTransactionAsync(transactionInput);
        }
    }
}