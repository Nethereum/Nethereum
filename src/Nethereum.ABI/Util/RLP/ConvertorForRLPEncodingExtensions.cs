using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Nethereum.ABI.Util.RLP
{
    public static class ConvertorForRLPEncodingExtensions
    {
        public static byte[] ToBytesForRLPEncoding(this BigInteger bigInteger)
        {
            return ToBytesFromNumber(bigInteger.ToByteArray());
        }

        public static byte[] ToBytesForRLPEncoding(this int number)
        {
            return ToBytesFromNumber(BitConverter.GetBytes(number));
        }

        public static byte[] ToBytesForRLPEncoding(this long number)
        {
            return ToBytesFromNumber(BitConverter.GetBytes(number));
        }

        public static byte[] ToBytesForRLPEncoding(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static byte[][] ToBytesForRLPEncoding(this string[] strings)
        {
            var output = new List<byte[]>();
            foreach (var str in strings)
            {
                output.Add(str.ToBytesForRLPEncoding());
            }
            return output.ToArray();
        }

        private static byte[] ToBytesFromNumber(byte[] bytes)
        {

            if (BitConverter.IsLittleEndian)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var trimmed = new List<byte>();
            bool previousByteWasZero = true;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (previousByteWasZero && bytes[i] == 0)
                {
                    continue;
                }

                previousByteWasZero = false;
                trimmed.Add(bytes[i]);
            }

            return trimmed.ToArray();
        }

        public static int ToIntFromRLPDecoded(this byte[] bytes)
        {
            return (int)ToBigIntegerFromRLPDecoded(bytes);
        }

        public static long ToLongFromRLPDecoded(this byte[] bytes)
        {
            return (long)ToBigIntegerFromRLPDecoded(bytes);
        }

        public static string ToStringFromRLPDecoded(this byte[] bytes)
        {
            if (bytes == null) return "";
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        public static BigInteger ToBigIntegerFromRLPDecoded(this byte[] bytes)
        {
            if (bytes == null) return 0;
            if (BitConverter.IsLittleEndian)
            {
                return new BigInteger(bytes.Reverse().ToArray());
            }
            return new BigInteger(bytes);
        }

    }
}