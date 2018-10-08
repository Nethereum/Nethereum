namespace Nethereum.Signer
{
    public class EthECDSASignatureFactory
    {
        public static EthECDSASignature FromComponents(byte[] r, byte[] s)
        {
            return new EthECDSASignature(ECDSASignatureFactory.FromComponents(r, s));
        }

        public static EthECDSASignature FromComponents(byte[] r, byte[] s, byte v)
        {
            var signature = FromComponents(r, s);
            signature.V = new[] {v};
            return signature;
        }

        public static EthECDSASignature FromComponents(byte[] r, byte[] s, byte[] v)
        {
            return new EthECDSASignature(ECDSASignatureFactory.FromComponents(r, s, v));
        }

        public static EthECDSASignature FromComponents(byte[] rs)
        {
            return new EthECDSASignature(ECDSASignatureFactory.FromComponents(rs));
        }
    }
}