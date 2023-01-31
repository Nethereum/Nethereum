namespace Nethereum.Model
{
    public abstract class SignedTypeTransaction : SignedTransaction
    {
        public override void SetSignature(ISignature signature)
        {
            Signature = signature;
        }
    }
}