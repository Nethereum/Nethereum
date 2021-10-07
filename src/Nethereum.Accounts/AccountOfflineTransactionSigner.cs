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
        private readonly LegacyTransactionSigner _legacyTransactionSigner;

        public AccountOfflineTransactionSigner(LegacyTransactionSigner legacyTransactionSigner)
        {
            _legacyTransactionSigner = legacyTransactionSigner;
        }

        public AccountOfflineTransactionSigner()
        {
            _legacyTransactionSigner = new LegacyTransactionSigner();
        }

        public string SignTransaction(Account account, TransactionInput transaction, BigInteger? overridingAccountChainId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (string.IsNullOrWhiteSpace(transaction.From))
                transaction.From = account.Address;
            else if (!transaction.From.IsTheSameAddress(account.Address))
                throw new Exception("Invalid account used for signing, does not match the transaction input");

            var chainId = overridingAccountChainId;
            if (chainId == null)
            {
                chainId = account.ChainId;
            }
            
            var nonce = transaction.Nonce;
            if (nonce == null)
                throw new ArgumentNullException(nameof(transaction), "Transaction nonce has not been set");

            var gasLimit = transaction.Gas;
            var value = transaction.Value ?? new HexBigInteger(0);
            string signedTransaction;

            if (transaction.Type != null && transaction.Type.Value == TransactionType.EIP1559.AsByte())
            {
                var maxPriorityFeePerGas = transaction.MaxPriorityFeePerGas.Value;
                var maxFeePerGas = transaction.MaxFeePerGas.Value;
                if (chainId == null) throw new ArgumentException("ChainId required for TransactionType 0X02 EIP1559");

                var transaction1559 = new Transaction1559(chainId.Value, nonce, maxPriorityFeePerGas, maxFeePerGas,
                    gasLimit, transaction.To, value, transaction.Data,
                    transaction.AccessList.ToSignerAccessListItemArray());
                transaction1559.Sign(new EthECKey(account.PrivateKey));
                signedTransaction = transaction1559.GetRLPEncoded().ToHex();
            }
            else
            {
                var gasPrice = transaction.GasPrice;

                if (chainId == null)
                    signedTransaction = _legacyTransactionSigner.SignTransaction(account.PrivateKey,
                        transaction.To,
                        value.Value, nonce,
                        gasPrice.Value, gasLimit.Value, transaction.Data);
                else
                    signedTransaction = _legacyTransactionSigner.SignTransaction(account.PrivateKey, chainId.Value,
                        transaction.To,
                        value.Value, nonce,
                        gasPrice.Value, gasLimit.Value, transaction.Data);
            }

            return signedTransaction;
        }
    }
}