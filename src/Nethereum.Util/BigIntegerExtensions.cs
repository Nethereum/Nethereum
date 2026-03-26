using System;
using System.Globalization;
using System.Numerics;

namespace Nethereum.Util
{
    public static class BigIntegerExtensions
    {
        public static int NumberOfDigits(this BigInteger value)
        {
            return (value * value.Sign).ToString().Length;
        }

        public static BigInteger ParseInvariant(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            return BigInteger.Parse(value, CultureInfo.InvariantCulture);
        }

        public static byte[] ToByteArrayUnsignedBigEndian(this BigInteger value)
        {
            var littleEndian = value.ToByteArray();
            int len = littleEndian.Length;
            if (len > 1 && littleEndian[len - 1] == 0)
                len--;
            var result = new byte[len];
            for (int i = 0; i < len; i++)
                result[i] = littleEndian[len - 1 - i];
            return result;
        }

        public static BigInteger ToBigIntegerFromUnsignedBigEndian(this byte[] bigEndian)
        {
            if (bigEndian == null || bigEndian.Length == 0)
                return BigInteger.Zero;
            var littleEndian = new byte[bigEndian.Length + 1];
            for (int i = 0; i < bigEndian.Length; i++)
                littleEndian[bigEndian.Length - 1 - i] = bigEndian[i];
            return new BigInteger(littleEndian);
        }
    }
}