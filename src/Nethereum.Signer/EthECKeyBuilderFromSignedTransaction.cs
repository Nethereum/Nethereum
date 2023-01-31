using Nethereum.Model;

namespace Nethereum.Signer
{
    public static class EthECKeyBuilderFromSignedTransaction
    {
        public static EthECKey GetKey(this ISignedTransaction transaction)
        {
            return GetEthECKey(transaction);
        }

        public static EthECKey GetEthECKey(this ISignedTransaction transaction)
        {
            var signature = EthECDSASignatureFactory.FromSignature(transaction.Signature);

            if (transaction is LegacyTransaction legacyTransaction)
            {
                return EthECKey.RecoverFromSignature(signature,
                                           legacyTransaction.RawHash
                                           );
            }

            if (transaction is Transaction1559 transaction1559)
            {
                return EthECKey.RecoverFromParityYSignature(signature, transaction.RawHash);
            }

            if (transaction is LegacyTransactionChainId legacyTransactionChainId)
            {
                return EthECKey.RecoverFromSignature(signature,
                                            legacyTransactionChainId.RawHash,
                                            legacyTransactionChainId.GetChainIdAsBigInteger());
            }

            throw new System.ArgumentOutOfRangeException("Argument: " + nameof(transaction) + " is not a supported transaction");

        }
    }
}