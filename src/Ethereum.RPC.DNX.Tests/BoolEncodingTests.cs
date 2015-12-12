using Ethereum.RPC.ABI;
using Ethereum.RPC.Util;
using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class BoolEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeTrue()
        {
            var boolType = new BoolType();
            var result = boolType.Encode(true).ToHexString();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", result);
        }

        [Fact]
        public virtual void ShouldEncodeFalse()
        {
            var boolType = new BoolType();
            var result = boolType.Encode(false).ToHexString();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", result);
        }

        [Fact]
        public virtual void ShouldDecodeFalse()
        {
            var boolType = new BoolType();
            var result = boolType.DecodeString("0000000000000000000000000000000000000000000000000000000000000000");
            Assert.Equal(false, result);
        }

        [Fact]
        public virtual void ShouldDecodeTrue()
        {
            var boolType = new BoolType();
            var result = boolType.DecodeString("0000000000000000000000000000000000000000000000000000000000000001");
            Assert.Equal(true, result);
        }
    }
}