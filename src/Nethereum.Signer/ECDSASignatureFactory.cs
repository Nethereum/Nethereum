using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using Org.BouncyCastle.Math;

namespace Nethereum.Signer
{
    public class ECDSASignatureFactory
    {
        public static ECDSASignature FromComponents(byte[] r, byte[] s)
        {
            return new ECDSASignature(new BigInteger(1, r), new BigInteger(1, s));
        }

        public static ECDSASignature FromComponents(byte[] r, byte[] s, byte v)
        {
            var signature = FromComponents(r, s);
            signature.V = new[] { v };
            return signature;
        }

        public static ECDSASignature FromComponents(byte[] r, byte[] s, byte[] v)
        {
            var signature = FromComponents(r, s);
            signature.V = v;
            return signature;
        }

        public static ECDSASignature FromComponents(byte[] rs)
        {
            var r = new byte[32];
            var s = new byte[32];
            Array.Copy(rs, 0, r, 0, 32);
            Array.Copy(rs, 32, s, 0, 32);
            var signature = FromComponents(r, s);
            return signature;
        }

        public static ECDSASignature ExtractECDSASignature(string signature)
        {
            var signatureArray = signature.HexToByteArray();
            return ExtractECDSASignature(signatureArray);
        }

        public static ECDSASignature ExtractECDSASignature(byte[] signatureArray)
        { 
            var v = signatureArray[64];

            if (v == 0 || v == 1)
                v = (byte)(v + 27);

            var r = new byte[32];
            Array.Copy(signatureArray, r, 32);
            var s = new byte[32];
            Array.Copy(signatureArray, 32, s, 0, 32);

            return ECDSASignatureFactory.FromComponents(r, s, v);
        }
    }
}