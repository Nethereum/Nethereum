using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.Tests
{
    public class IntEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeStrings()
        {
            IntType intType = new IntType("int");
            var result2 = intType.Encode("1234567890abcdef1234567890abcdef12345678").ToHex();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }

        [Fact]
        public virtual void ShouldEncodeInt()
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(69).ToHex();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000045", result);
        }

        [Fact]
        public virtual void ShouldEncodeNegativeInt()
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(-1234567).ToHex();
            Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979", result);
        }

        [Fact]
        public virtual void ShouldEncodeUInt()
        {
            IntType intType = new IntType("uint");
            uint given = 1234567;
            var result = intType.Encode(given).ToHex();
            Assert.Equal("000000000000000000000000000000000000000000000000000000000012d687", result);
        }

        [Fact]
        public virtual void ShouldDecodeString()
        {
            IntType intType = new IntType("int");
            var result = intType.Decode<BigInteger>("0x00000000000000000000000000000000000000000000000000000000000001e3");
            Assert.Equal(new BigInteger(483), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeIntString()
        {
            IntType intType = new IntType("int");
            var result = intType.Decode<BigInteger>("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979");
            Assert.Equal(new BigInteger(-1234567), result);
        }

        public BigInteger ToTwosComplement(BigInteger value)
        {
            if (value.Sign < 0)
            {
                return new BigInteger("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".HexToByteArray()) + value + 1;
            }
            return value;
        }

        [Fact]
        public void Test()
        {
            Debug.WriteLine(ToTwosComplement(-37797598375987353).ToByteArray().Reverse().ToArray().ToHex());
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffc4653600"));
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffffff8ee84e68e144"));
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffff79b748d76fb767"));
        }

        public BigInteger FromTwosComplement(string value)
        {
           return new BigInteger(value.HexToByteArray().Reverse().ToArray()) -
            new BigInteger("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".HexToByteArray()) - 1;
        }

        [Theory]
        [InlineData("-1000000000", "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffc4653600")]
        [InlineData("-124346577657532", "0xffffffffffffffffffffffffffffffffffffffffffffffffffff8ee84e68e144")]
        [InlineData("127979392992", "0x0000000000000000000000000000000000000000000000000000001dcc2a8fe0")]
        [InlineData("-37797598375987353", "0xffffffffffffffffffffffffffffffffffffffffffffffffff79b748d76fb767")]
        [InlineData("3457987492347979798742", "0x0000000000000000000000000000000000000000000000bb75377716692498d6")]
        public virtual void ShouldDecode(string expected, string hex)
        {
            IntType intType = new IntType("int");
            var result = intType.Decode<BigInteger>(hex);
            Assert.Equal(expected, result.ToString());
        }



        [Theory]
        [InlineData("-1000000000", "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffc4653600")]
        [InlineData("-124346577657532", "0xffffffffffffffffffffffffffffffffffffffffffffffffffff8ee84e68e144")]
        [InlineData("127979392992", "0x0000000000000000000000000000000000000000000000000000001dcc2a8fe0")]
        [InlineData("-37797598375987353", "0xffffffffffffffffffffffffffffffffffffffffffffffffff79b748d76fb767")]
        [InlineData("3457987492347979798742", "0x0000000000000000000000000000000000000000000000bb75377716692498d6")]
        public virtual void ShouldEncode(string value, string hexExpected)
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(BigInteger.Parse(value));
            Assert.Equal(hexExpected, "0x" + result.ToHex());

        }

        [Fact]
        public virtual void ShouldDecodeByteArray()
        {
            IntType intType = new IntType("int");
            var bytes = intType.Encode(100000569);
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(100000569), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeByteArray()
        {
            IntType intType = new IntType("int");
            var bytes = intType.Encode(-100000569);
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(-100000569), result);
        }

        [Fact]
        public virtual void ShouldDecode0x989680()
        {
            IntType intType = new IntType("int");
            var bytes = "0x00989680".HexToByteArray();
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(10000000), result);
        }

        [Fact]
        public virtual void ShouldDecodeStringLength()
        {
            IntType intType = new IntType("int");
            var result = intType.Decode<BigInteger>("0x0000000000000000000000000000000000000000000000000000000000000020");
            Assert.Equal(new BigInteger(32), result);
        }

       

    }
}