using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Nethereum.EVM
{
    internal static class HexText
    {
#if NET8_0_OR_GREATER
        private static readonly uint[] Pairs = BuildPairs();

        private static uint[] BuildPairs()
        {
            const string digits = "0123456789abcdef";
            var pairs = new uint[256];
            for (int b = 0; b < 256; b++)
            {
                uint high = digits[b >> 4];
                uint low = digits[b & 15];
                pairs[b] = System.BitConverter.IsLittleEndian ? high | (low << 16) : (high << 16) | low;
            }
            return pairs;
        }
#endif

        public static string FromMemory(List<byte> memory, int index, int length)
        {
#if NET8_0_OR_GREATER
            if (length == 0) return string.Empty;
            return string.Create(length * 2, (memory, index), static (chars, state) =>
            {
                var source = CollectionsMarshal.AsSpan(state.memory).Slice(state.index, chars.Length / 2);
                var target = MemoryMarshal.Cast<char, uint>(chars);
                var pairs = Pairs;
                for (int i = 0; i < source.Length; i++) target[i] = pairs[source[i]];
            });
#else
            return MemoryBuffer.Read(memory, index, length).ToHex();
#endif
        }

        public static string FromWord(EvmUInt256 word)
        {
#if NET8_0_OR_GREATER
            return string.Create(64, word, static (chars, value) =>
            {
                var target = MemoryMarshal.Cast<char, uint>(chars);
                var pairs = Pairs;
                WriteLimb(target, 0, value.U3, pairs);
                WriteLimb(target, 8, value.U2, pairs);
                WriteLimb(target, 16, value.U1, pairs);
                WriteLimb(target, 24, value.U0, pairs);
            });
#else
            return word.ToBigEndian().ToHex();
#endif
        }

#if NET8_0_OR_GREATER
        private static void WriteLimb(System.Span<uint> target, int offset, ulong limb, uint[] pairs)
        {
            for (int i = 0; i < 8; i++) target[offset + i] = pairs[(int)((limb >> (56 - 8 * i)) & 0xff)];
        }
#endif
    }
}
