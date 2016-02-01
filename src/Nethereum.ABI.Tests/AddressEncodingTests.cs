using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.Tests
{
    public class AddressEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeAddressString()
        {
            AddressType addressType = new AddressType();
            var result2 = addressType.Encode("1234567890abcdef1234567890abcdef12345678").ToHex();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }
    }
}