using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class ByteArrayToHexTests
    {
        [Fact]
        public void Given_bytes_When_ToHex_Then_the_lowercase_digits_are_returned_without_a_prefix()
        {
            Assert.Equal("00ff1aa5", new byte[] { 0x00, 0xff, 0x1a, 0xa5 }.ToHex());
        }

        [Fact]
        public void Given_bytes_When_ToHex_with_prefix_Then_the_digits_follow_0x()
        {
            Assert.Equal("0x00ff1aa5", new byte[] { 0x00, 0xff, 0x1a, 0xa5 }.ToHex(true));
        }

        [Fact]
        public void Given_no_bytes_When_ToHex_Then_the_result_is_empty_or_just_the_prefix()
        {
            Assert.Equal(string.Empty, new byte[0].ToHex());
            Assert.Equal("0x", new byte[0].ToHex(true));
        }
    }
}
