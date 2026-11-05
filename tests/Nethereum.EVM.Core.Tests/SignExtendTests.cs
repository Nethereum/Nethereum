using System;
using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class SignExtendTests
    {
        private static readonly BigInteger Two256 = BigInteger.One << 256;

        private static EvmUInt256 SignExtend(BigInteger width, BigInteger value)
        {
            var program = new Program(new byte[] { 0x00 });
            program.StackPush((EvmUInt256)value);
            program.StackPush((EvmUInt256)width);
            new EvmBitwiseExecution().SignExtend(program);
            return program.StackPopU256();
        }

        private static BigInteger Reference(BigInteger width, BigInteger value)
        {
            if (width >= 31) return value;
            int bits = (int)(width + 1) * 8;
            var low = value & ((BigInteger.One << bits) - 1);
            return (low >> (bits - 1)).IsOne ? (low - (BigInteger.One << bits) + Two256) % Two256 : low;
        }

        private static BigInteger Random256(Random random)
        {
            var bytes = new byte[33];
            random.NextBytes(bytes);
            bytes[32] = 0;
            if (random.Next(4) == 0) Array.Clear(bytes, random.Next(32), 1);
            return new BigInteger(bytes);
        }

        [Fact]
        public void Given_any_byte_index_and_value_When_SIGNEXTEND_Then_the_result_equals_the_BigInteger_reference()
        {
            var random = new Random(11);
            var values = new System.Collections.Generic.List<BigInteger>
            {
                0, 1, 0x7f, 0x80, 0xff, 0x8000, 0xFFDADADA, Two256 - 1, BigInteger.One << 255, (BigInteger.One << 255) - 1
            };
            for (int i = 0; i < 2000; i++) values.Add(Random256(random));

            foreach (var value in values)
                for (int width = 0; width <= 40; width++)
                    Assert.Equal(Reference(width, value), (BigInteger)SignExtend(width, value));
        }

        [Fact]
        public void Given_a_byte_index_of_two_to_the_256_minus_one_When_SIGNEXTEND_Then_the_value_is_unchanged()
        {
            Assert.Equal((BigInteger)0x80, (BigInteger)SignExtend(Two256 - 1, 0x80));
        }

        [Fact]
        public void Given_byte_index_three_and_a_negative_32_bit_value_When_SIGNEXTEND_Then_the_upper_bytes_are_ones()
        {
            Assert.Equal(Two256 - 0x252526, (BigInteger)SignExtend(3, 0xFFDADADA));
        }
    }
}
