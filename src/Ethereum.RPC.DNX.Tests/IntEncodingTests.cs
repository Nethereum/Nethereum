using System.Numerics;
using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class IntEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeStrings()
        {
            IntType intType = new IntType("int");
            var result2 = intType.Encode("1234567890abcdef1234567890abcdef12345678").ToHexString();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }

        [Fact]
        public virtual void ShouldEncodeInt()
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(69).ToHexString();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000045", result);
        }

      

        [Fact]
        public virtual void ShouldDecodeString()
        {
            IntType intType = new IntType("int");
            var result = intType.DecodeString("0x00000000000000000000000000000000000000000000000000000000000001e3");
            Assert.Equal(new BigInteger(483), result);
        }

       
        [Fact]
        public virtual void ShouldDecodeStringLength()
        {
            IntType intType = new IntType("int");
            var result = intType.DecodeString("0x0000000000000000000000000000000000000000000000000000000000000020");
            Assert.Equal(new BigInteger(32), result);
        }
    }
}