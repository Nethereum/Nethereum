using System;
using System.Numerics;
using Ethereum.RPC.Util;

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

        public static BigInteger ConvertHexToBigInteger(this string hex)
        {
            return new BigInteger(hex.HexStringToByteArray());
        }
    }
}