using Nethereum.Model;

namespace Nethereum.Signer
{

    public static class TransactionVerificationAndRecovery
    {
        public static byte[] GetPublicKey(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).GetPublicKey();
        }

        public static string GetSenderAddress(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).GetSenderAddress();
        }

        public static bool VerifyTransaction(string rlp)
        {
            return TransactionFactory.CreateTransaction(rlp).VerifyTransaction();
        }

        public static byte[] GetPublicKey(this ISignedTransaction transaction)
        {
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).GetPubKey();
        }

        public static string GetSenderAddress(this ISignedTransaction transaction)
        {
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).GetPublicAddress();
        }

        public static bool VerifyTransaction(this ISignedTransaction transaction)
        {
            var signature = EthECDSASignatureFactory.FromSignature(transaction.Signature);
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).VerifyAllowingOnlyLowS(transaction.RawHash, signature);
        }
    }
}