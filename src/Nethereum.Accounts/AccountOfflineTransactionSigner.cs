using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Web3.Accounts
{
    public class AccountOfflineTransactionSigner
    {
        private readonly LegacyTransactionSigner _legacyTransactionSigner;
        private readonly Transaction1559Signer _transaction1559Signer;
        private readonly Transaction7702Signer _transaction7702Signer;
        private readonly Transaction4844Signer _transaction4844Signer;

        public AccountOfflineTransactionSigner(LegacyTransactionSigner legacyTransactionSigner, Transaction1559Signer transaction1559Signer, Transaction7702Signer transaction7702Signer, Transaction4844Signer transaction4844Signer)
        {
            _legacyTransactionSigner = legacyTransactionSigner;
            _transaction1559Signer = transaction1559Signer;
            _transaction7702Signer = transaction7702Signer;
            _transaction4844Signer = transaction4844Signer;
        }

        public AccountOfflineTransactionSigner()
        {
            _legacyTransactionSigner = new LegacyTransactionSigner();
            _transaction1559Signer = new Transaction1559Signer();
            _transaction7702Signer = new Transaction7702Signer();
            _transaction4844Signer = new Transaction4844Signer();
        }

        public string SignTransaction(Account account, TransactionInput transaction, BigInteger? overridingAccountChainId = null)
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
                _transaction1559Signer.SignTransaction(new EthECKey(account.PrivateKey), transaction1559);
                signedTransaction = transaction1559.GetRLPEncoded().ToHex();
            }
            else if (transaction.Type != null && transaction.Type.Value == TransactionType.EIP7702.AsByte())
            {
                var maxPriorityFeePerGas = transaction.MaxPriorityFeePerGas.Value;
                var maxFeePerGas = transaction.MaxFeePerGas.Value;
                if (chainId == null) throw new ArgumentException("ChainId required for TransactionType 0X04 EIP7702");

                var transaction7702 = new Transaction7702(chainId.Value, nonce, maxPriorityFeePerGas, maxFeePerGas,
                    gasLimit, transaction.To, value, transaction.Data,
                    transaction.AccessList.ToSignerAccessListItemArray(), transaction.AuthorisationList.ToAuthorisation7720SignedList());
                _transaction7702Signer.SignTransaction(new EthECKey(account.PrivateKey), transaction7702);
                signedTransaction = transaction7702.GetRLPEncoded().ToHex();
            }
            else if (transaction.Type != null && transaction.Type.Value == TransactionType.Blob.AsByte())
            {
                var maxPriorityFeePerGas = transaction.MaxPriorityFeePerGas.Value;
                var maxFeePerGas = transaction.MaxFeePerGas.Value;
                var maxFeePerBlobGas = transaction.MaxFeePerBlobGas?.Value ?? BigInteger.Zero;
                if (chainId == null) throw new ArgumentException("ChainId required for TransactionType 0X03 EIP4844");

                var blobHashes = new System.Collections.Generic.List<byte[]>();
                if (transaction.BlobVersionedHashes != null)
                {
                    foreach (var hash in transaction.BlobVersionedHashes)
                        blobHashes.Add(hash.HexToByteArray());
                }

                var transaction4844 = new Transaction4844(chainId.Value, nonce, maxPriorityFeePerGas, maxFeePerGas,
                    gasLimit, transaction.To, value, transaction.Data,
                    transaction.AccessList.ToSignerAccessListItemArray(), maxFeePerBlobGas, blobHashes);
                _transaction4844Signer.SignTransaction(new EthECKey(account.PrivateKey), transaction4844);
                signedTransaction = transaction4844.GetRLPEncoded().ToHex();
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