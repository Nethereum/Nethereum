namespace Nethereum.Signer
{
    public abstract class SignedTypeTransaction : SignedTransaction
    {
        public override void Sign(EthECKey key)
        {
            Signature = key.SignAndCalculateYParityV(RawHash);
        }

        public override EthECKey Key => EthECKey.RecoverFromParityYSignature(Signature, RawHash);
    }
}