using System;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class TransactionVerificationAndRecoveryImp : ITransactionVerificationAndRecovery
    {
        public byte[] GetPublicKey(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).GetPublicKey();
        }

        public string GetSenderAddress(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).GetSenderAddress();
        }

        public bool VerifyTransaction(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).VerifyTransaction();
        }

        public byte[] GetPublicKey(ISignedTransaction transaction)
        {
            return RecoverKey(transaction).GetPubKey();
        }

        public string GetSenderAddress(ISignedTransaction transaction)
        {
            return RecoverKey(transaction).GetPublicAddress();
        }

        public bool VerifyTransaction(ISignedTransaction transaction)
        {
            var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction);
            if (key == null)
            {
                return false;
            }

            var signature = EthECDSASignatureFactory.FromSignature(transaction.Signature);
            if (transaction.TransactionType != TransactionType.LegacyTransaction
                && transaction.TransactionType != TransactionType.LegacyChainTransaction
                && !HasCanonicalTypedSignature(transaction, signature))
            {
                return false;
            }

            return key.VerifyAllowingOnlyLowS(transaction.RawHash, signature);
        }

        private static bool HasCanonicalTypedSignature(ISignedTransaction transaction, EthECDSASignature signature)
        {
            var v = transaction.Signature.V;
            if (v == null || v.Length > 1 || (v.Length == 1 && v[0] > 1))
            {
                return false;
            }

            return signature.IsCanonical;
        }

        private static EthECKey RecoverKey(ISignedTransaction transaction)
        {
            var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction);
            if (key == null)
            {
                throw new InvalidOperationException("The transaction signature does not recover a sender.");
            }
            return key;
        }
    }
}