using System;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public static class ModExpHeaderParser
    {
        public static ModExpHeader Parse(byte[] input)
        {
            var data = input ?? new byte[0];

            int offset = 0;
            var baseLen = ReadHeaderWord(data, offset); offset += 32;
            var expLen  = ReadHeaderWord(data, offset); offset += 32;
            var modLen  = ReadHeaderWord(data, offset); offset += 32;

            EvmUInt256 expHead = EvmUInt256.Zero;

            if (!expLen.IsZero && expLen.FitsInInt)
            {
                int headLen = Math.Min(32, expLen.ToInt());

                long expStartOffsetLong = (long)offset +
                    (baseLen.FitsInInt ? (long)baseLen.ToInt() : (long)int.MaxValue);

                int expStartOffset = expStartOffsetLong < data.Length
                    ? (int)expStartOffsetLong
                    : data.Length;
                for (int i = 0; i < headLen; i++)
                {
                    int idx = expStartOffset + i;
                    byte b = idx < data.Length ? data[idx] : (byte)0;
                    expHead = (expHead << 8) | new EvmUInt256(b);
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

        private static EvmUInt256 ReadHeaderWord(byte[] data, int offset)
        {
            if (offset >= data.Length) return EvmUInt256.Zero;
            int available = Math.Min(32, data.Length - offset);

            var buf = new byte[32];
            Array.Copy(data, offset, buf, 0, available);
            return EvmUInt256.FromBigEndian(buf);
        }
    }
}
