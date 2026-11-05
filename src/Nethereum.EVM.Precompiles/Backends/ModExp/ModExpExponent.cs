#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
using System.Numerics;

namespace Nethereum.EVM.Precompiles.Backends.ModExp
{
    internal sealed class ModExpExponent
    {
        private readonly byte[] _bigEndian;

        public ModExpExponent(byte[] bigEndian)
        {
            _bigEndian = bigEndian;
            BitLength = MeasureBitLength(bigEndian);
        }

        public byte[] BigEndian => _bigEndian;

        public int BitLength { get; }

        public bool IsZero => BitLength == 0;

        public bool Bit(int index)
        {
            int byteIndex = _bigEndian.Length - 1 - (index >> 3);
            return byteIndex >= 0 && ((_bigEndian[byteIndex] >> (index & 7)) & 1) != 0;
        }

        public int Window(int lowBit, int width)
        {
            int value = 0;
            for (int i = width - 1; i >= 0; i--)
                value = (value << 1) | (Bit(lowBit + i) ? 1 : 0);
            return value;
        }

        private static int MeasureBitLength(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0) continue;
                return (bytes.Length - i - 1) * 8 + 32 - BitOperations.LeadingZeroCount((uint)bytes[i]);
            }
            return 0;
        }
    }
}

#endif
