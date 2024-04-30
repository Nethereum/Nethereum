using Nethereum.Model;

namespace Nethereum.Signer
{

    public static class TransactionVerificationAndRecovery
    {

        private static ITransactionVerificationAndRecovery _transactionVerificationAndRecovery = new TransactionVerificationAndRecoveryImp();

        public static byte[] GetPublicKey(string rlp)
        {
            return _transactionVerificationAndRecovery.GetPublicKey(rlp);
        }

        public static string GetSenderAddress(string rlp)
        {
           return _transactionVerificationAndRecovery.GetSenderAddress(rlp);
        }

        public static bool VerifyTransaction(string rlp)
        {
            return _transactionVerificationAndRecovery.VerifyTransaction(rlp);
        }

        public static byte[] GetPublicKey(this ISignedTransaction transaction)
        {
           return _transactionVerificationAndRecovery.GetPublicKey(transaction);
        }

        public static string GetSenderAddress(this ISignedTransaction transaction)
        {
           return _transactionVerificationAndRecovery.GetSenderAddress(transaction);
        }

        public static bool VerifyTransaction(this ISignedTransaction transaction)
        {
           return _transactionVerificationAndRecovery.VerifyTransaction(transaction);
        }
    }
}