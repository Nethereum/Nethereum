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
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).GetPubKey();
        }

        public string GetSenderAddress(ISignedTransaction transaction)
        {
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).GetPublicAddress();
        }

        public bool VerifyTransaction(ISignedTransaction transaction)
        {
            var signature = EthECDSASignatureFactory.FromSignature(transaction.Signature);
            return EthECKeyBuilderFromSignedTransaction.GetEthECKey(transaction).VerifyAllowingOnlyLowS(transaction.RawHash, signature);
        }

    }
}