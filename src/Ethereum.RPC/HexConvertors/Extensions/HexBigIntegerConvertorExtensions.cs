using System;
using System.Linq;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC
{
    public static class HexBigIntegerConvertorExtensions
    {
        public static string ToHex(this BigInteger value, bool littleEndian)
        {
            byte[] bytes;

            if (BitConverter.IsLittleEndian != littleEndian)
            {
                bytes = value.ToByteArray().Reverse().ToArray();
            }
            else
            {
                bytes = value.ToByteArray().ToArray();
            }

            return bytes.ToHex();
        }


        public static BigInteger HexToBigInteger(this string hex, bool isHexLittleEndian)
        {
            var encoded = hex.HexToByteArray();

            if ((BitConverter.IsLittleEndian != isHexLittleEndian))
            {
                encoded = encoded.ToArray().Reverse().ToArray();
            }
            return new BigInteger(encoded);
        }

        
    }
}