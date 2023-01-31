using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Model
{

    public abstract class SignedLegacyTransactionBase: SignedTransaction
    {
        protected RLPSignedDataHashBuilder RlpSignerEncoder { get; set; }

        public override byte[] RawHash => RlpSignerEncoder.RawHash;
        public override ISignature Signature  => RlpSignerEncoder.Signature;
            
        public override byte[] GetRLPEncoded()
        {
            return RlpSignerEncoder.GetRLPEncoded();
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return RlpSignerEncoder.GetRLPEncodedRaw();
        }

        protected static string ToHex(byte[] x)
        {
            if (x == null) return "0x";
            return x.ToHex();
        }

        public override void SetSignature(ISignature signature)
        {
            RlpSignerEncoder.SetSignature(signature);
        }
    }
}