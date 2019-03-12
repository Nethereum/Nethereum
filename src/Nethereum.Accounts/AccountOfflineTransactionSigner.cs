using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Web3.Accounts
{
    public class AccountOfflineTransactionSigner
    {
        private readonly TransactionSigner _transactionSigner;

        public AccountOfflineTransactionSigner(TransactionSigner transactionSigner)
        {
            _transactionSigner = transactionSigner;
        }

        public AccountOfflineTransactionSigner()
        {
            _transactionSigner = new TransactionSigner();
        }

        public string SignTransaction(Account account, TransactionInput transaction, BigInteger? chainId = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (string.IsNullOrWhiteSpace(transaction.From))
            {
                transaction.From = account.Address;
            }
            else if (!transaction.From.IsTheSameAddress(account.Address))
            {
                throw new Exception("Invalid account used for signing, does not match the transaction input");
            }

            var nonce = transaction.Nonce;
            if (nonce == null) throw new ArgumentNullException(nameof(transaction), "Transaction nonce has not been set");

            var gasPrice = transaction.GasPrice;
            var gasLimit = transaction.Gas;

            var value = transaction.Value ?? new HexBigInteger(0);

            string signedTransaction;

            if (chainId == null)
            {
                signedTransaction = _transactionSigner.SignTransaction(account.PrivateKey,
                    transaction.To,
                    value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data);
            }
            else
            {
                signedTransaction = _transactionSigner.SignTransaction(account.PrivateKey, chainId.Value,
                    transaction.To,
                    value.Value, nonce,
                    gasPrice.Value, gasLimit.Value, transaction.Data);
            }

            return signedTransaction;
        }
    }
}