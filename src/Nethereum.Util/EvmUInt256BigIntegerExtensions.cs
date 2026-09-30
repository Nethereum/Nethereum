using System;
using System.Numerics;

namespace Nethereum.Util
{
    /// <summary>
    /// BigInteger interop for EvmUInt256.
    /// Separated from EvmUInt256 to avoid pulling System.Runtime.Numerics into Zisk binaries.
    /// </summary>
    public static class EvmUInt256BigIntegerExtensions
    {
        public static BigInteger ToBigInteger(this EvmUInt256 value)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new BigInteger(value.ToBigEndian(), isUnsigned: true, isBigEndian: true);
#else
            var bytes = value.ToBigEndian();
            var le = new byte[33];
            for (int i = 0; i < 32; i++)
                le[31 - i] = bytes[i];
            le[32] = 0;
            return new BigInteger(le);
#endif
        }

        public static EvmUInt256 FromBigInteger(BigInteger value)
        {
            if (value.Sign < 0)
                value += BigIntegerExtensions.TWO_256;
#if NETCOREAPP3_0_OR_GREATER
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
#else
            var bytes = value.ToByteArray();
            Array.Reverse(bytes);
            if (bytes.Length > 0 && bytes[0] == 0)
            {
                var trimmed = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, trimmed, 0, trimmed.Length);
                bytes = trimmed;
            }
#endif
            return EvmUInt256.FromBigEndian(bytes);
        }
    }
}
