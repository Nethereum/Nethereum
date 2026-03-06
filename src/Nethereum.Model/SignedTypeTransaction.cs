namespace Nethereum.Model
{
    public abstract class SignedTypeTransaction : SignedTransaction
    {
        internal byte[] OriginalRlpEncoded { get; set; }

        public override void SetSignature(ISignature signature)
        {
            Signature = signature;
            OriginalRlpEncoded = null;
        }
    }
}