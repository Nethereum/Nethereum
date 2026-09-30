using System;
using Nethereum.RLP;

namespace Nethereum.Util
{
    public static class EvmUInt256RLPExtensions
    {
        public static byte[] ToBytesForRLPEncoding(this EvmUInt256 value)
        {
            if (value.IsZero) return new byte[0];
            return ConvertorForRLPEncodingExtensions.TrimZeroBytes(value.ToBigEndian());
        }

        public static EvmUInt256 ToEvmUInt256FromRLPDecoded(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return EvmUInt256.Zero;
            var padded = new byte[32];
            var offset = 32 - bytes.Length;
            if (offset < 0) offset = 0;
            var len = bytes.Length > 32 ? 32 : bytes.Length;
            Array.Copy(bytes, 0, padded, offset, len);
            return EvmUInt256.FromBigEndian(padded);
        }
    }
}
