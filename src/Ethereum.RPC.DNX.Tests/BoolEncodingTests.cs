using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class StringEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeString()
        {
            var monkeyEncoded =
                "00000000000000000000000000000000000000000000000000000000000000064d6f6e6b65790000000000000000000000000000000000000000000000000000";

            //0000000000000000000000000000000000000000000000000000000000000006 is the bytes length
            //4d6f6e6b65790000000000000000000000000000000000000000000000000000 Monkey byte array utf8 encoded
            
            var stringType = new StringType();
            var result = stringType.Encode("Monkey").ToHexString();
            Assert.Equal(monkeyEncoded, result);
        }

       
    }

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