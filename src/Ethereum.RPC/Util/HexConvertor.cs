using System;
using System.Collections.Generic;
using System.Numerics;
using Ethereum.RPC.Util;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ethereum.RPC
{
    public static class HexConvertor
    {
        public static Int64 ConvertHexToInt64(this string hex)
        {
            return Convert.ToInt64(hex, 16);
        }

        public static Int64? ConvertHexToNullableInt64(this string hex)
        {
            return hex?.ConvertHexToInt64();
        }

        public static string ConvertInt64ToHex(this Int64? input)
        {
            return $"0x{input:X}";
        }

        public static BigInteger ConvertBigEndianHexToBigInteger(this string hex)
        {
            var encoded = hex.HexStringToByteArray();

            if (BitConverter.IsLittleEndian)
            {
                encoded = encoded.ToArray().Reverse().ToArray();
            }
            return new BigInteger(encoded);
        }
    }
}