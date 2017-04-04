using Org.BouncyCastle.Math;

namespace Nethereum.Signer
{
    public class EthECDSASignatureFactory
    {
        public static EthECDSASignature FromComponents(byte[] r, byte[] s)
        {
            return new EthECDSASignature(new BigInteger(1, r), new BigInteger(1, s));
        }

        public static EthECDSASignature FromComponents(byte[] r, byte[] s, byte v)
        {
            var signature = FromComponents(r, s);
            signature.V = v;
            return signature;
        }
    }
}