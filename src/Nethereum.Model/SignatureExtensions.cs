using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using System;

namespace Nethereum.Model
{
    public static class SignatureExtensions
    {
        public static bool IsVSignedForChain(this ISignature signature)
        {
            return signature.V.ToBigIntegerFromRLPDecoded() >= 35;
        }

        public static bool IsVSignedForLegacy(this ISignature signature)
        {
            var v = signature.V.ToBigIntegerFromRLPDecoded();
            return v >= 27;
        }

        public static bool IsVSignedForYParity(this ISignature signature)
        {
            var v = signature.V.ToBigIntegerFromRLPDecoded();
            return v == 0 || v == 1;
        }

        public static byte[] To64ByteArray(this ISignature signature)
        {
            var rsigPad = new byte[32];
            Array.Copy(signature.R, 0, rsigPad, rsigPad.Length - signature.R.Length, signature.R.Length);

            var ssigPad = new byte[32];
            Array.Copy(signature.S, 0, ssigPad, ssigPad.Length - signature.S.Length, signature.S.Length);

            return ByteUtil.Merge(rsigPad, ssigPad);
        }

        public static string CreateStringSignature(this ISignature signature)
        {
            return "0x" + signature.R.ToHex().PadLeft(64, '0') +
                   signature.S.ToHex().PadLeft(64, '0') +
                   signature.V.ToHex();
        }
    }
}