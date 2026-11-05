using System;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    public class ModExpHeaderParserDifferentialTests
    {
        [Fact]
        public void Given_random_modexp_inputs_When_the_header_is_parsed_Then_it_equals_the_byte_at_a_time_reading_of_the_header()
        {
            var rng = new Random(20261022);
            for (int i = 0; i < 5000; i++)
            {
                var input = RandomInput(rng);

                var actual = ModExpHeaderParser.Parse(input);
                var expected = ReferenceParse(input);

                Assert.Equal(expected.BaseLen, actual.BaseLen);
                Assert.Equal(expected.ExpLen, actual.ExpLen);
                Assert.Equal(expected.ModLen, actual.ModLen);
                Assert.Equal(expected.ExpHead, actual.ExpHead);
                Assert.Equal(expected.ExpBitLen, actual.ExpBitLen);
            }
        }

        [Fact]
        public void Given_a_null_input_When_the_header_is_parsed_Then_every_field_is_zero()
        {
            var header = ModExpHeaderParser.Parse(null);

            Assert.True(header.BaseLen.IsZero && header.ExpLen.IsZero && header.ModLen.IsZero && header.ExpHead.IsZero);
            Assert.Equal(0, header.ExpBitLen);
        }

        private static byte[] RandomInput(Random rng)
        {
            var length = rng.Next(5) == 0 ? rng.Next(100) : 96 + rng.Next(220);
            var input = new byte[length];
            rng.NextBytes(input);
            for (int word = 0; word < 3; word++)
            {
                int offset = word * 32;
                if (offset + 32 > length) continue;
                int kind = rng.Next(4);
                if (kind == 0) Array.Clear(input, offset, 32);
                else if (kind == 1) { Array.Clear(input, offset, 31); input[offset + 31] = (byte)rng.Next(80); }
                else if (kind == 2) Array.Clear(input, offset, 28);
            }
            return input;
        }

        private static (EvmUInt256 BaseLen, EvmUInt256 ExpLen, EvmUInt256 ModLen, EvmUInt256 ExpHead, int ExpBitLen) ReferenceParse(byte[] data)
        {
            var baseLen = ReferenceWord(data, 0);
            var expLen = ReferenceWord(data, 32);
            var modLen = ReferenceWord(data, 64);
            var expHead = EvmUInt256.Zero;
            if (!expLen.IsZero && expLen.FitsInInt)
            {
                int headLen = Math.Min(32, expLen.ToInt());
                long expStart = 96L + (baseLen.FitsInInt ? baseLen.ToInt() : int.MaxValue);
                int start = expStart < data.Length ? (int)expStart : data.Length;
                for (int i = 0; i < headLen; i++)
                {
                    int idx = start + i;
                    byte b = idx < data.Length ? data[idx] : (byte)0;
                    expHead = (expHead << 8) | new EvmUInt256(b);
                }
            }
            int bits = 0;
            for (var temp = expHead; !temp.IsZero; temp = temp >> 1) bits++;
            return (baseLen, expLen, modLen, expHead, bits);
        }

        private static EvmUInt256 ReferenceWord(byte[] data, int offset)
        {
            if (offset >= data.Length) return EvmUInt256.Zero;
            var buffer = new byte[32];
            Array.Copy(data, offset, buffer, 0, Math.Min(32, data.Length - offset));
            return EvmUInt256.FromBigEndian(buffer);
        }
    }
}
