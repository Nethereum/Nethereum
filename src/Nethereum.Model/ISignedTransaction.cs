namespace Nethereum.Model
{
    public interface ISignedTransaction
    {
        TransactionType TransactionType {get; }
        ISignature Signature { get; }
        byte[] RawHash { get; }
        byte[] Hash { get; }
        byte[] GetRLPEncoded();
        byte[] GetRLPEncodedRaw();
        void SetSignature(ISignature signature);
    }
}