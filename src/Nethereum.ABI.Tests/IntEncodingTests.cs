using System;
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
        public virtual void ShouldEncodeDecodeInt()
        {
            IntType intType = new IntType("int");
            var result = intType.Encode(Int32.MaxValue).ToHex();
            var intresult = intType.Decode<int>(result);
            Assert.Equal(Int32.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeInt64()
        {
            IntType intType = new IntType("int64");
            var result = intType.Encode(Int64.MaxValue).ToHex();
            var intresult = intType.Decode<long>(result);
            Assert.Equal(Int64.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeUInt64()
        {
            IntType intType = new IntType("uint64");
            var result = intType.Encode(UInt64.MaxValue).ToHex();
            var intresult = intType.Decode<UInt64>(result);
            Assert.Equal(UInt64.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeUShort()
        {
            IntType intType = new IntType("uint16");
            var result = intType.Encode(ushort.MaxValue).ToHex();
            var intresult = intType.Decode<ushort>(result);
            Assert.Equal(ushort.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeShort()
        {
            IntType intType = new IntType("int16");
            var result = intType.Encode(short.MaxValue).ToHex();
            var intresult = intType.Decode<short>(result);
            Assert.Equal(short.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeByte()
        {
            IntType intType = new IntType("uint8");
            var result = intType.Encode(byte.MaxValue).ToHex();
            var intresult = intType.Decode<byte>(result);
            Assert.Equal(byte.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeSByte()
        {
            IntType intType = new IntType("int8");
            var result = intType.Encode(sbyte.MaxValue).ToHex();
            var intresult = intType.Decode<sbyte>(result);
            Assert.Equal(sbyte.MaxValue, intresult);
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