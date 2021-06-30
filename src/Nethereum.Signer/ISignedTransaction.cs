namespace Nethereum.Signer
{
    public interface ISignedTransaction
    {
        TransactionType TransactionType {get; }
        EthECDSASignature Signature { get; }
        EthECKey Key { get; }
        byte[] RawHash { get; }
        byte[] Hash { get; }
        void Sign(EthECKey key);
        byte[] GetRLPEncoded();
        byte[] GetRLPEncodedRaw();
    }
}