using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmAddressFromHexTests
    {
        private static EvmAddress Normalised(string address) =>
            EvmAddress.From(AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant().HexToByteArray());

        [Theory]
        [InlineData("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266")]
        [InlineData("0xF39FD6E51AAD88F6F4CE6AB8827279CFFFB92266")]
        [InlineData("f39fd6e51aad88f6f4ce6ab8827279cfffb92266")]
        [InlineData("0x0000000000000000000000000000000000000001")]
        [InlineData("0x1")]
        [InlineData("0x01")]
        [InlineData("1")]
        [InlineData("0xabc")]
        [InlineData("0x00000000000000000000000000000000000000000000000000000000000000ff")]
        [InlineData("0xff00000000000000000000000000000000000000000000000000000000000001")]
        [InlineData("0x")]
        public void Given_AnAddressInAnySpelling_When_ParsedFromHex_Then_ItEqualsTheNormalisedParse(string address)
        {
            Assert.Equal(Normalised(address), EvmAddress.FromHex(address));
        }

        [Theory]
        [InlineData("0xzz39Fd6e51aad88F6F4ce6aB8827279cffFb92266")]
        [InlineData("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb9226g")]
        [InlineData("f39Fd6e51aad88F6F4ce6aB8827279cffFb9226g")]
        public void Given_AnAddressWithANonHexDigit_When_ParsedFromHex_Then_ItThrowsLikeTheNormalisedParse(string address)
        {
            Assert.Throws<FormatException>(() => Normalised(address));
            Assert.Throws<FormatException>(() => EvmAddress.FromHex(address));
        }

        [Fact]
        public void Given_NullOrEmpty_When_ParsedFromHex_Then_ItIsTheZeroAddress()
        {
            Assert.Equal(EvmAddress.Zero, EvmAddress.FromHex(null));
            Assert.Equal(EvmAddress.Zero, EvmAddress.FromHex(""));
        }

        [Fact]
        public void Given_RandomAddresses_When_ParsedFromHex_Then_TheyEqualTheNormalisedParse()
        {
            var random = new System.Random(11);
            var bytes = new byte[20];
            for (var i = 0; i < 500; i++)
            {
                random.NextBytes(bytes);
                var hex = bytes.ToHex(i % 2 == 0);
                Assert.Equal(Normalised(hex), EvmAddress.FromHex(hex));
                Assert.Equal(Normalised(hex), EvmAddress.FromHex(hex.ToUpperInvariant().Replace("0X", "0x")));
            }
        }
    }
}
