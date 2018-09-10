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
    public class ExternalAccountSignerTransactionManager : TransactionManagerBase
    {
        private readonly TransactionSigner _transactionSigner;
        public BigInteger? ChainId { get; private set; }

        public ExternalAccountSignerTransactionManager(IClient rpcClient, ExternalAccount account, BigInteger? chainId = null)
        {
            ChainId = chainId;
            Account = account ?? throw new ArgumentNullException(nameof(account));
            Client = rpcClient;
            _transactionSigner = new TransactionSigner();
        }


        public override BigInteger DefaultGas { get; set; } = Transaction.DEFAULT_GAS_LIMIT;


        public override Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return SignAndSendTransactionAsync(transactionInput);
        }

        public override Task<string> SignTransactionAsync(TransactionInput transaction)
        {
            return SignTransactionRetrievingNextNonceAsync(transaction);
        }

        public async Task<string> SignTransactionExternallyAsync(TransactionInput transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!string.Equals(transaction.From.EnsureHexPrefix(), Account.Address.EnsureHexPrefix(), StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Invalid account used signing");
            SetDefaultGasPriceAndCostIfNotSet(transaction);

            var nonce = transaction.Nonce;
            if (nonce == null) throw new ArgumentNullException(nameof(transaction), "Transaction nonce has not been set");

            var gasPrice = transaction.GasPrice;
            var gasLimit = transaction.Gas;

            var value = transaction.Value ?? new HexBigInteger(0);

            string signedTransaction;

            var externalSigner = ((ExternalAccount) Account).ExternalSigner;
            if (ChainId == null)
            {
                signedTransaction = await _transactionSigner.SignTransactionAsync(externalSigner,
                    transaction.To,
                    value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data).ConfigureAwait(false);
            }
            else
            {
                signedTransaction = await _transactionSigner.SignTransactionAsync(externalSigner, ChainId.Value,
                    transaction.To,
                    value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data);
            }

            return signedTransaction;
        }


        public string SignTransaction(TransactionInput transaction)
        {
            return SignTransactionRetrievingNextNonceAsync(transaction).Result;
        }

        protected async Task<string> SignTransactionRetrievingNextNonceAsync(TransactionInput transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.From.EnsureHexPrefix().ToLower() != Account.Address.EnsureHexPrefix().ToLower())
                throw new Exception("Invalid account used signing");
            var nonce = await GetNonceAsync(transaction).ConfigureAwait(false);
            transaction.Nonce = nonce;
            var gasPrice = await GetGasPriceAsync(transaction).ConfigureAwait(false);
            transaction.GasPrice = gasPrice;
            return await SignTransactionExternallyAsync(transaction).ConfigureAwait(false);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
            {
                if (Account.NonceService == null)
                    Account.NonceService = new InMemoryNonceService(Account.Address, Client);
                Account.NonceService.Client = Client;
                nonce = await Account.NonceService.GetNextNonceAsync().ConfigureAwait(false);
            }
            return nonce;
        }

        private async Task<string> SignAndSendTransactionAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.From.EnsureHexPrefix().ToLower() != Account.Address.EnsureHexPrefix().ToLower())
                throw new Exception("Invalid account used signing");

            var ethSendTransaction = new EthSendRawTransaction(Client);
            var signedTransaction = await SignTransactionRetrievingNextNonceAsync(transaction).ConfigureAwait(false);
            return await ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }
    }
}