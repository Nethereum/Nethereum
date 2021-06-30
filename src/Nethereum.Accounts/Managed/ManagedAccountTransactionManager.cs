using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Personal;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Web3.Accounts.Managed
{
    public class ManagedAccountTransactionManager : TransactionManagerBase
    {
        public ManagedAccountTransactionManager(IClient client, ManagedAccount account)
        {
            Account = account;
            Client = client;
        }

        public ManagedAccountTransactionManager(IClient client, string accountAddress, string password)
        {
            Account = new ManagedAccount(accountAddress, password, this);
            Client = client;
        }

        public ManagedAccountTransactionManager(string accountAddress, string password) : this(null, accountAddress,
            password)
        {
        }

        public override BigInteger DefaultGas { get; set; } = LegacyTransaction.DEFAULT_GAS_LIMIT;

        public void SetAccount(ManagedAccount account)
        {
            Account = account;
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
                if (Account.NonceService != null)
                {
                    Account.NonceService.Client = Client;
                    nonce = await Account.NonceService.GetNextNonceAsync().ConfigureAwait(false);
                }
            return nonce;
        }


        public override async Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (!transactionInput.From.IsTheSameAddress(Account.Address)) throw new Exception("Invalid account used");
            
            await SetTransactionFeesOrPricing(transactionInput);
            SetDefaultGasPriceAndCostIfNotSet(transactionInput);

            var nonce = await GetNonceAsync(transactionInput).ConfigureAwait(false);
            if (nonce != null) transactionInput.Nonce = nonce;
            var ethSendTransaction = new PersonalSignAndSendTransaction(Client);
            return await ethSendTransaction.SendRequestAsync(transactionInput, ((ManagedAccount) Account).Password)
                .ConfigureAwait(false);
        }

        public override Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            if (!from.IsTheSameAddress(Account.Address)) throw new Exception("Invalid account used");
            var transactionInput = new TransactionInput(null, to, from, null, null, amount);
            return SendTransactionAsync(transactionInput);
        }

        public override Task<string> SignTransactionAsync(TransactionInput transaction)
        {
            throw new InvalidOperationException("Managed accounts cannot sign offline transactions");
        }

    }
}