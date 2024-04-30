namespace Nethereum.Model
{
    public interface ITransactionVerificationAndRecovery
    {
        byte[] GetPublicKey(ISignedTransaction transaction);
        byte[] GetPublicKey(string rlp);
        string GetSenderAddress(ISignedTransaction transaction);
        string GetSenderAddress(string rlp);
        bool VerifyTransaction(ISignedTransaction transaction);
        bool VerifyTransaction(string rlp);
    }
}