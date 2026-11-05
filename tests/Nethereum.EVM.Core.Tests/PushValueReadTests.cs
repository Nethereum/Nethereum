using System;
using System.Linq;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class PushValueReadTests
    {
        private static byte[] PushCode(int width, int presentBytes)
        {
            var code = new byte[1 + presentBytes];
            code[0] = (byte)(0x5f + width);
            for (var i = 0; i < presentBytes; i++) code[1 + i] = (byte)(0xa0 + i + 1);
            return code;
        }

        private static EvmUInt256 ExecutePush(byte[] code)
        {
            var program = new Program(code);
            new EvmProgramStackFlowExecution().Push(program);
            return program.StackPopU256();
        }

        private static EvmUInt256 ZeroExtendedCodeValue(byte[] code, int width)
        {
            var padded = new byte[width];
            Array.Copy(code, 1, padded, 0, Math.Min(width, code.Length - 1));
            return EvmUInt256.FromBigEndian(padded);
        }

        [Theory]
        [MemberData(nameof(AllWidths))]
        public void Given_PUSHn_with_all_its_data_When_executed_Then_the_pushed_value_is_the_big_endian_data(int width)
        {
            var code = PushCode(width, width);

            Assert.Equal(EvmUInt256.FromBigEndian(code.Skip(1).ToArray()), ExecutePush(code));
        }

        [Theory]
        [MemberData(nameof(AllWidths))]
        public void Given_PUSHn_cut_off_by_the_end_of_code_When_executed_Then_the_missing_bytes_are_zero_and_the_present_bytes_are_the_high_bytes(int width)
        {
            var code = PushCode(width, width - 1);

            var expected = ZeroExtendedCodeValue(code, width);
            Assert.Equal(expected, ExecutePush(code));
            Assert.Equal(EvmUInt256.FromBigEndian(code.Skip(1).Concat(new byte[] { 0 }).ToArray()), expected);
        }

        [Fact]
        public void Given_PUSH32_with_a_single_byte_before_the_end_of_code_When_executed_Then_that_byte_is_the_most_significant_byte_of_the_word()
        {
            var value = ExecutePush(new byte[] { 0x7f, 0xab });

            Assert.Equal("ab00000000000000000000000000000000000000000000000000000000000000", value.ToBigEndian().ToHex());
            Assert.NotEqual(new EvmUInt256(0xab), value);
        }

        [Fact]
        public void Given_PUSH2_with_no_data_at_all_When_executed_Then_zero_is_pushed()
        {
            Assert.Equal(EvmUInt256.Zero, ExecutePush(new byte[] { 0x61 }));
        }

        [Theory]
        [MemberData(nameof(AllWidths))]
        public void Given_any_PUSHn_When_the_slice_is_read_Then_it_equals_the_value_built_from_the_decoded_immediate(int width)
        {
            foreach (var present in new[] { width, width - 1, 0 })
            {
                var code = PushCode(width, present);
                var program = new Program(code);

                Assert.Equal(EvmUInt256.FromBigEndian(program.CurrentImmediate), program.CurrentPushValue);
            }
        }

        [Fact]
        public void Given_a_slice_of_a_larger_array_When_the_word_is_read_Then_only_the_slice_is_used_and_the_end_of_the_array_reads_as_zeros()
        {
            var source = new byte[] { 0xff, 0x01, 0x02, 0x03, 0xff };

            Assert.Equal(EvmUInt256.FromBigEndian(new byte[] { 1, 2, 3 }), EvmUInt256.FromBigEndian(source, 1, 3));
            Assert.Equal(EvmUInt256.FromBigEndian(new byte[] { 3, 0xff, 0, 0 }), EvmUInt256.FromBigEndian(source, 3, 4));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(source, 9, 4));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(source, 0, 0));
        }

        [Fact]
        public void Given_a_length_over_thirty_two_When_the_word_is_read_Then_it_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EvmUInt256.FromBigEndian(new byte[40], 0, 33));
        }

        public static System.Collections.Generic.IEnumerable<object[]> AllWidths()
        {
            return Enumerable.Range(1, 32).Select(width => new object[] { width });
        }
    }
}
