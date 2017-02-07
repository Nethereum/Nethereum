using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;

namespace Nethereum.Signer
{
    public class EthECDSASignatureFactory
    {
        public static ECDSASignature FromComponents(byte[] r, byte[] s)
        {
            return new ECDSASignature(new BigInteger(1, r), new BigInteger(1, s));
        }

        public static ECDSASignature FromComponents(byte[] r, byte[] s, byte v)
        {
            var signature = FromComponents(r, s);
            signature.V = v;
            return signature;
        }
    }
}