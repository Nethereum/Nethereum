using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class IntEncodingTests
    {
        public BigInteger ToTwosComplement(BigInteger value)
        {
            if (value.Sign < 0)
                return new BigInteger("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
                           .HexToByteArray()) + value + 1;
            return value;
        }

        public BigInteger FromTwosComplement(string value)
        {
            return new BigInteger(value.HexToByteArray().Reverse().ToArray()) -
                   new BigInteger("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
                       .HexToByteArray()) - 1;
        }

        [Theory]
        [InlineData("-1000000000", "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffc4653600")]
        [InlineData("-124346577657532", "0xffffffffffffffffffffffffffffffffffffffffffffffffffff8ee84e68e144")]
        [InlineData("127979392992", "0x0000000000000000000000000000000000000000000000000000001dcc2a8fe0")]
        [InlineData("-37797598375987353", "0xffffffffffffffffffffffffffffffffffffffffffffffffff79b748d76fb767")]
        [InlineData("3457987492347979798742", "0x0000000000000000000000000000000000000000000000bb75377716692498d6")]
        public virtual void ShouldDecode(string expected, string hex)
        {
            var intType = new IntType("int");
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
            var intType = new IntType("int");
            var result = intType.Encode(BigInteger.Parse(value));
            Assert.Equal(hexExpected, "0x" + result.ToHex());
        }

        [Fact]
        public virtual void ShouldDecode0x989680()
        {
            var intType = new IntType("int");
            var bytes = "0x00989680".HexToByteArray();
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(10000000), result);
        }

        [Fact]
        public virtual void ShouldDecodeByteArray()
        {
            var intType = new IntType("int");
            var bytes = intType.Encode(100000569);
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(100000569), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeByteArray()
        {
            var intType = new IntType("int");
            var bytes = intType.Encode(-100000569);
            var result = intType.Decode<BigInteger>(bytes);
            Assert.Equal(new BigInteger(-100000569), result);
        }

        [Fact]
        public virtual void ShouldDecodeNegativeIntString()
        {
            var intType = new IntType("int");
            var result =
                intType.Decode<BigInteger>("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979");
            Assert.Equal(new BigInteger(-1234567), result);
        }

        [Fact]
        public virtual void ShouldDecodeString()
        {
            var intType = new IntType("int");
            var result =
                intType.Decode<BigInteger>("0x00000000000000000000000000000000000000000000000000000000000001e3");
            Assert.Equal(new BigInteger(483), result);
        }

        [Fact]
        public virtual void ShouldDecodeStringLength()
        {
            var intType = new IntType("int");
            var result =
                intType.Decode<BigInteger>("0x0000000000000000000000000000000000000000000000000000000000000020");
            Assert.Equal(new BigInteger(32), result);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeByte()
        {
            var intType = new IntType("uint8");
            var result = intType.Encode(byte.MaxValue).ToHex();
            var intresult = intType.Decode<byte>(result);
            Assert.Equal(byte.MaxValue, intresult);
        }


        [Fact]
        public virtual void ShouldEncodeDecodeInt()
        {
            var intType = new IntType("int");
            var result = intType.Encode(int.MaxValue).ToHex();
            var intresult = intType.Decode<int>(result);
            Assert.Equal(int.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeInt64()
        {
            var intType = new IntType("int64");
            var result = intType.Encode(long.MaxValue).ToHex();
            var intresult = intType.Decode<long>(result);
            Assert.Equal(long.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeSByte()
        {
            var intType = new IntType("int8");
            var result = intType.Encode(sbyte.MaxValue).ToHex();
            var intresult = intType.Decode<sbyte>(result);
            Assert.Equal(sbyte.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeShort()
        {
            var intType = new IntType("int16");
            var result = intType.Encode(short.MaxValue).ToHex();
            var intresult = intType.Decode<short>(result);
            Assert.Equal(short.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeUInt64()
        {
            var intType = new IntType("uint64");
            var result = intType.Encode(ulong.MaxValue).ToHex();
            var intresult = intType.Decode<ulong>(result);
            Assert.Equal(ulong.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeDecodeUShort()
        {
            var intType = new IntType("uint16");
            var result = intType.Encode(ushort.MaxValue).ToHex();
            var intresult = intType.Decode<ushort>(result);
            Assert.Equal(ushort.MaxValue, intresult);
        }

        [Fact]
        public virtual void ShouldEncodeInt()
        {
            var intType = new IntType("int");
            var result = intType.Encode(69).ToHex();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000045", result);
        }

        [Fact]
        public virtual void ShouldEncodeNegativeInt()
        {
            var intType = new IntType("int");
            var result = intType.Encode(-1234567).ToHex();
            Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed2979", result);
        }

        [Fact]
        public virtual void ShouldEncodeStrings()
        {
            var intType = new IntType("int");
            var result2 = intType.Encode("1234567890abcdef1234567890abcdef12345678").ToHex();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }

        [Fact]
        public virtual void ShouldEncodeUInt()
        {
            var intType = new IntType("uint");
            uint given = 1234567;
            var result = intType.Encode(given).ToHex();
            Assert.Equal("000000000000000000000000000000000000000000000000000000000012d687", result);
        }

        [Fact]
        public virtual void ShouldEncodeNullableUInt()
        {
            var intType = new IntType("uint");
            uint? given = 1234567;
            var result = intType.Encode(given).ToHex();
            Assert.Equal("000000000000000000000000000000000000000000000000000000000012d687", result);
        }

        [Fact]
        public virtual void ShouldEncodeNullableUIntNull()
        {
            var intType = new IntType("uint");
            uint? given = null;
            var result = intType.Encode(given).ToHex();
            Assert.Equal("", result);
        }

        public virtual void ShouldThrowErrorWhileEncodeLargeInt()
        {
            const int maxIntSizeInBytes = 32;
            var intType = new IntType("uint");
            var given = new BigInteger(Enumerable.Range(1, maxIntSizeInBytes + 1).Select(x => (byte) x).ToArray());
            var ex = Assert.Throws<ArgumentOutOfRangeException>("value", () => intType.Encode(given));
            Assert.StartsWith($"Integer value must not exceed maximum Solidity size of {maxIntSizeInBytes} bytes", ex.Message);
        }

        [Fact]
        public void Test()
        {
            Debug.WriteLine(ToTwosComplement(-37797598375987353).ToByteArray().Reverse().ToArray().ToHex());
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffc4653600"));
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffffff8ee84e68e144"));
            Debug.WriteLine(FromTwosComplement("0xffffffffffffffffffffffffffffffffffffffffffffffffff79b748d76fb767"));
        }
    }
}