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
            var value = CachedInt256Type.Decode<BigInteger>(bytes);

            if (value > IntType.MAX_INT256_VALUE)
            {
                value = 1 + IntType.MAX_UINT256_VALUE - value;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigInteger ConvertToUInt256(this byte[] bytes)
        {
            return CachedUInt256Type.Decode<BigInteger>(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ConvertToABIBytes(this BigInteger value)
        {
            return CachedUInt256Type.Encode(value);
        }

    }
}