using System;
using System.Linq;
using System.Numerics;

namespace Nethereum.Hex.HexConvertors.Extensions
{
    public static class HexBigIntegerConvertorExtensions
    {
        public static string ToHex(this BigInteger value, bool littleEndian)
        {
            if (value == 0) return "0x0";

            byte[] bytes;

            if (BitConverter.IsLittleEndian != littleEndian)
            {
                bytes = value.ToByteArray().Reverse().ToArray();
            }
            else
            {
                bytes = value.ToByteArray().ToArray();
            }

            return "0x" + bytes.ToHexCompact();
        }


        public static BigInteger HexToBigInteger(this string hex, bool isHexLittleEndian)
        {
            var encoded = hex.HexToByteArray();

            if ((BitConverter.IsLittleEndian != isHexLittleEndian))
            {
                var listEncoded = encoded.ToList();
                listEncoded.Insert(0, 0x00);
                encoded = listEncoded.ToArray().Reverse().ToArray();

            }
            return new BigInteger(encoded);
        }

        
    }
}