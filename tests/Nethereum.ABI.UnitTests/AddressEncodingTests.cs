using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class AddressEncodingTests
    {
        [Fact]
        public virtual void ShouldDecodeAddressString()
        {
            var addressType = new AddressType();
            var result2 =
                addressType.Decode("0000000000000000000000001234567890abcdef1234567890abcdef12345678".HexToByteArray(),
                    typeof(string));
            Assert.True("0x1234567890abcdef1234567890abcdef12345678".IsTheSameAddress(result2.ToString()));
        }

        [Fact]
        public virtual void ShouldEncodeAddressString()
        {
            var addressType = new AddressType();
            var result2 = addressType.Encode("1234567890abcdef1234567890abcdef12345678").ToHex();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }


        [Fact]
        public virtual void ShouldEncodeDecodeAddressString()
        {
            var addressType = new AddressType();
            var result2 = addressType.Encode("0034567890abcdef1234567890abcdef12345678").ToHex();
            var result3 = addressType.Decode(result2, typeof(string));
            Assert.True("0x0034567890abcdef1234567890abcdef12345678".IsTheSameAddress(result3.ToString()));
        }
    }
}