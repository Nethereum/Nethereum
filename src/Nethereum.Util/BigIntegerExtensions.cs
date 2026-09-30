using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nethereum.Util
{
    public static class BigIntegerExtensions
    {
        public static readonly BigInteger MAX_UINT256_VALUE = BigInteger.Pow(2, 256) - 1;
        public static readonly BigInteger MAX_INT256_VALUE = BigInteger.Pow(2, 255) - 1;
        public static readonly BigInteger TWO_256 = BigInteger.Pow(2, 256);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigInteger ConvertToEvmUInt256(this byte[] bytes)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
#else
            return bytes.ToBigIntegerFromUnsignedBigEndian();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigInteger ConvertToEvmInt256(this byte[] bytes)
        {
#if NETCOREAPP3_0_OR_GREATER
            var padded = bytes;
            if (bytes.Length < 32)
            {
                padded = new byte[32];
                Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            }
            var value = new BigInteger(padded, isUnsigned: true, isBigEndian: true);
#else
            var value = bytes.ToBigIntegerFromUnsignedBigEndian();
#endif
            if (value > MAX_INT256_VALUE)
            {
                value -= TWO_256;
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToEvmUInt256ByteArray(this BigInteger value)
        {
            var bytes = value.ToByteArrayUnsignedBigEndian();
            var result = new byte[32];
            if (bytes.Length <= 32)
                Array.Copy(bytes, 0, result, 32 - bytes.Length, bytes.Length);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToEvmSignedUInt256ByteArray(this BigInteger value)
        {
            if (value < 0)
                value = TWO_256 + value;
            return value.ToEvmUInt256ByteArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] EncodeAddressTo32Bytes(string address)
        {
            return AddressUtil.EncodeAddressTo32Bytes(address);
        }

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
            if (value.Sign < 0)
                throw new ArgumentException("Value must be non-negative for unsigned conversion", nameof(value));
            if (value.Sign == 0)
                return new byte[] { 0 };
            // BigInteger.ToByteArray() always returns little-endian per .NET spec,
            // with a trailing 0x00 if the high bit is set (sign byte for positive values)
            var littleEndian = value.ToByteArray();
            int len = littleEndian.Length;
            if (littleEndian[len - 1] == 0)
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