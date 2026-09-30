using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nethereum.ABI.Decoders
{
    public static class ByteUtilExtensions
    {
        private static readonly IntType CachedInt256Type = new IntType("int256");
        private static readonly IntType CachedUInt256Type = new IntType("uint256");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigInteger ConvertToInt256(this byte[] bytes)
        {
#if NETCOREAPP3_0_OR_GREATER
            var padded = bytes;
            if (bytes.Length < 32)
            {
                padded = new byte[32];
                System.Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            }
            var value = new BigInteger(padded, isUnsigned: true, isBigEndian: true);
#else
            var value = CachedInt256Type.Decode<BigInteger>(bytes);
#endif
            if (value > IntType.MAX_INT256_VALUE)
            {
                value -= (IntType.MAX_UINT256_VALUE + 1);
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigInteger ConvertToUInt256(this byte[] bytes)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
#else
            return CachedUInt256Type.Decode<BigInteger>(bytes);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ConvertToABIBytes(this BigInteger value)
        {
            return CachedUInt256Type.Encode(value);
        }

    }
}