using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class BoolEncodingTests
    {
        [Fact]
        public virtual void ShouldDecodeFalse()
        {
            var boolType = new BoolType();
            var result = boolType.Decode<bool>("0000000000000000000000000000000000000000000000000000000000000000");
            Assert.Equal(false, result);
        }

        [Fact]
        public virtual void ShouldDecodeTrue()
        {
            var boolType = new BoolType();
            var result = boolType.Decode<bool>("0000000000000000000000000000000000000000000000000000000000000001");
            Assert.Equal(true, result);
        }

        [Fact]
        public virtual void ShouldEncodeFalse()
        {
            var boolType = new BoolType();
            var result = boolType.Encode(false).ToHex();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", result);
        }

        [Fact]
        public virtual void ShouldEncodeTrue()
        {
            var boolType = new BoolType();
            var result = boolType.Encode(true).ToHex();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", result);
        }
    }
}