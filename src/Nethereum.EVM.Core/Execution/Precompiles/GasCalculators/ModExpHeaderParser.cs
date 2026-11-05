using System;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public static class ModExpHeaderParser
    {
        private const int HeaderWordLength = 32;

        public static ModExpHeader Parse(byte[] input)
        {
            var data = input ?? new byte[0];

            var baseLen = EvmUInt256.FromBigEndian(data, 0, HeaderWordLength);
            var expLen = EvmUInt256.FromBigEndian(data, HeaderWordLength, HeaderWordLength);
            var modLen = EvmUInt256.FromBigEndian(data, 2 * HeaderWordLength, HeaderWordLength);
            var expHead = ReadExponentHead(data, baseLen, expLen);

            return new ModExpHeader(baseLen, expLen, modLen, expHead, expHead.BitLength());
        }

        private static EvmUInt256 ReadExponentHead(byte[] data, EvmUInt256 baseLen, EvmUInt256 expLen)
        {
            if (expLen.IsZero || !expLen.FitsInInt) return EvmUInt256.Zero;

            int headLen = Math.Min(HeaderWordLength, expLen.ToInt());
            long expStart = 3L * HeaderWordLength + (baseLen.FitsInInt ? baseLen.ToInt() : int.MaxValue);
            int start = expStart < data.Length ? (int)expStart : data.Length;
            return EvmUInt256.FromBigEndian(data, start, headLen);
        }
    }
}
