using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256FromBigEndianSliceTests
    {
        private static byte[] Source(int length)
        {
            var source = new byte[length];
            new System.Random(length + 17).NextBytes(source);
            return source;
        }

        private static BigInteger Reference(byte[] source, int offset, int length)
        {
            var padded = new byte[length];
            var available = Math.Max(0, Math.Min(length, source.Length - offset));
            Array.Copy(source, offset, padded, 0, available);
            return new BigInteger(padded, isUnsigned: true, isBigEndian: true);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(31)]
        [InlineData(32)]
        public void Given_a_slice_inside_the_source_When_read_Then_it_equals_the_big_endian_number_of_those_bytes(int length)
        {
            var source = Source(70);

            foreach (var offset in new[] { 0, 1, 5, 38 })
            {
                var value = EvmUInt256.FromBigEndian(source, offset, length);

                Assert.Equal(Reference(source, offset, length), value.ToBigInteger());
            }
        }

        [Theory]
        [InlineData(1, 0, 1)]
        [InlineData(1, 1, 2)]
        [InlineData(10, 4, 20)]
        [InlineData(10, 9, 32)]
        [InlineData(5, 5, 32)]
        [InlineData(40, 30, 32)]
        public void Given_a_slice_running_past_the_end_of_the_source_When_read_Then_the_missing_low_bytes_are_zero(int sourceLength, int offset, int length)
        {
            var source = Source(sourceLength);

            var value = EvmUInt256.FromBigEndian(source, offset, length);

            Assert.Equal(Reference(source, offset, length), value.ToBigInteger());
        }

        [Fact]
        public void Given_a_null_source_When_read_Then_the_value_is_zero()
        {
            Assert.True(EvmUInt256.FromBigEndian(null, 0, 32).IsZero);
        }

        [Fact]
        public void Given_a_length_over_32_When_read_Then_it_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EvmUInt256.FromBigEndian(new byte[40], 0, 33));
        }
    }
}
