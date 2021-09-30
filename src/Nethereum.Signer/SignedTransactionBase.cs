using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Signer
{

    public abstract class SignedLegacyTransactionBase: SignedTransaction
    {
        protected RLPSigner SimpleRlpSigner { get; set; }

        public override byte[] RawHash => SimpleRlpSigner.RawHash;

        public override EthECDSASignature Signature => SimpleRlpSigner.Signature;
        

        public override byte[] GetRLPEncoded()
        {
            return SimpleRlpSigner.GetRLPEncoded();
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return SimpleRlpSigner.GetRLPEncodedRaw();
        }

        public override void Sign(EthECKey key)
        {
            SimpleRlpSigner.SignLegacy(key);
        }

        public override void SetSignature(EthECDSASignature signature)
        {
            SimpleRlpSigner.SetSignature(signature);
        }

        protected static string ToHex(byte[] x)
        {
            if (x == null) return "0x";
            return x.ToHex();
        }
#if !DOTNET35
        public abstract override Task SignExternallyAsync(IEthExternalSigner externalSigner);
#endif
    }
}