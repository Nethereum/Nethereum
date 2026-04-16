using System;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// Parses the fixed 96-byte MODEXP header (three uint256 lengths) and
    /// extracts the top 32 bytes of the exponent. Returns
    /// <see cref="EvmUInt256"/> instead of
    /// <c>System.Numerics.BigInteger</c> so the parsed header stays on
    /// the Nethereum.EVM.Core hot path without pulling
    /// <c>BigInteger</c> in.
    /// </summary>
    public static class ModExpHeaderParser
    {
        public static ModExpHeader Parse(byte[] input)
        {
            var data = input ?? new byte[0];

            int offset = 0;
            var baseLen = ReadUInt256BigEndian(data, offset, 32); offset += 32;
            var expLen  = ReadUInt256BigEndian(data, offset, 32); offset += 32;
            var modLen  = ReadUInt256BigEndian(data, offset, 32); offset += 32;

            EvmUInt256 expHead = EvmUInt256.Zero;

            if (!expLen.IsZero && expLen.FitsInInt)
            {
                int headLen = Math.Min(32, expLen.ToInt());

                // Compute the exponent start offset in 64-bit so that a
                // pathological baseLen near or beyond int.MaxValue (e.g.
                // EIP-7823 over-limit inputs) cannot overflow the int32
                // arithmetic and produce a negative index. When the
                // computed offset lies past the end of the input buffer
                // the exponent bytes simply aren't present, so expHead
                // stays zero — matching the "zero-pad missing input"
                // semantics used elsewhere in the header parser.
                long expStartOffsetLong = (long)offset +
                    (baseLen.FitsInInt ? (long)baseLen.ToInt() : (long)int.MaxValue);

                if (expStartOffsetLong < data.Length)
                {
                    int expStartOffset = (int)expStartOffsetLong;
                    for (int i = 0; i < headLen && (expStartOffset + i) < data.Length; i++)
                    {
                        expHead = (expHead << 8) | new EvmUInt256(data[expStartOffset + i]);
                    }
                }
            }

            int expBitLen = 0;
            if (!expHead.IsZero)
            {
                var temp = expHead;
                while (!temp.IsZero) { expBitLen++; temp = temp >> 1; }
            }

            return new ModExpHeader(baseLen, expLen, modLen, expHead, expBitLen);
        }

        /// <summary>
        /// Reads up to <paramref name="length"/> bytes from <paramref name="data"/>
        /// starting at <paramref name="offset"/> and interprets the result as a
        /// big-endian unsigned integer. When fewer bytes are available the read
        /// bytes are treated as the low-order bytes of a smaller number —
        /// matching the legacy <c>CancunPrecompileGasSchedule.ReadBigEndian</c>
        /// behaviour exactly so parity tests pass.
        /// </summary>
        private static EvmUInt256 ReadUInt256BigEndian(byte[] data, int offset, int length)
        {
            if (offset >= data.Length) return EvmUInt256.Zero;
            int available = Math.Min(length, data.Length - offset);

            // Place the available bytes at the low-order end of a 32-byte
            // big-endian buffer. Equivalent to treating the read bytes as a
            // number of magnitude (2^8)^available — i.e. they are the least
            // significant bytes of the value, not the most significant. This
            // matches legacy BigInteger(Reverse(data[offset..offset+available]))
            // which produces the same number.
            var buf = new byte[32];
            Array.Copy(data, offset, buf, 32 - available, available);
            return EvmUInt256.FromBigEndian(buf);
        }
    }
}
