using System.Numerics;
using Ethereum.RPC.ABI;
using Ethereum.RPC.Util;
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
        public virtual void ShouldEncodeNegativeInt()
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(-1234567).ToHexString();
            Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979", result);
        }

        [Fact]
        public virtual void ShouldDecodeString()
        {
            IntType intType = new IntType("int");
            var result = intType.DecodeString("0x00000000000000000000000000000000000000000000000000000000000001e3");
            Assert.Equal(new BigInteger(483), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeIntString()
        {
            IntType intType = new IntType("int");
            var result = intType.DecodeString("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979");
            Assert.Equal(new BigInteger(-1234567), result);
        }

        [Fact]
        public virtual void ShouldDecodeByteArray()
        {
            IntType intType = new IntType("int");
            var bytes = intType.Encode(100000569);
            var result = intType.Decode(bytes);
            Assert.Equal(new BigInteger(100000569), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeByteArray()
        {
            IntType intType = new IntType("int");
            var bytes = intType.Encode(-100000569);
            var result = intType.Decode(bytes);
            Assert.Equal(new BigInteger(-100000569), result);
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