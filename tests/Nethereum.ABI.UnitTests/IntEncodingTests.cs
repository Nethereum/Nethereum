using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class IntEncodingTests
    {
        public enum TestEnum
        {
            Monkey,
            Elephant,
            Lion
        }

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
        public virtual void ShouldEncodeDecodeEnum()
        {
            var intType = new IntType("int");
            var result1 = intType.Encode(TestEnum.Monkey).ToHex();
            var decresult1 = intType.Decode<TestEnum>(result1);
            Assert.Equal(TestEnum.Monkey, decresult1);

            var result2 = intType.Encode(TestEnum.Elephant).ToHex();
            var decresult2 = intType.Decode<TestEnum>(result2);
            Assert.Equal(TestEnum.Elephant, decresult2);

            var result3 = intType.Encode(TestEnum.Lion).ToHex();
            var decresult3 = intType.Decode<TestEnum>(result3);
            Assert.Equal(TestEnum.Lion, decresult3);
        }

        [Theory]
        [InlineData(int.MaxValue)]
        [InlineData(int.MaxValue / 3)]
        [InlineData(0)]
        [InlineData(int.MinValue / 3)]
        [InlineData(int.MinValue)]
        public virtual void ShouldEncodeDecodeInt(int value)
        {
            var intType = new IntType("int");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<int>(result);
            Assert.Equal(value, intresult);
        }

        [Theory]
        [InlineData(long.MaxValue)]
        [InlineData(long.MaxValue / 3)]
        [InlineData(0)]
        [InlineData(long.MinValue / 3)]
        [InlineData(long.MinValue)]
        public virtual void ShouldEncodeDecodeInt64(long value)
        {
            var intType = new IntType("int64");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<long>(result);
            Assert.Equal(value, intresult);
        }

        [Theory]
        [InlineData(long.MaxValue)]
        [InlineData(long.MaxValue / 3)]
        [InlineData(0)]
        [InlineData(long.MinValue / 3)]
        [InlineData(long.MinValue)]
        public virtual void ShouldEncodeDecodeInt128(long value)
        {
            Int128 value128 = (Int128) value;
            var intType = new IntType("int128");
            var result = intType.Encode(value128).ToHex();
            var intresult = intType.Decode<Int128>(result);
            Assert.Equal(value128, intresult);

            value128 <<= 64;
            result = intType.Encode(value128).ToHex();
            intresult = intType.Decode<Int128>(result);
            Assert.Equal(value128, intresult);
        }

        [Theory]
        [InlineData(ulong.MaxValue)]
        [InlineData(1)]
        [InlineData(0)]
        public virtual void ShouldEncodeDecodeUInt128(ulong value)
        {
            UInt128 value128 = (UInt128) value;
            var intType = new IntType("uint128");
            var result = intType.Encode(value128).ToHex();
            var intresult = intType.Decode<UInt128>(result);
            Assert.Equal(value128, intresult);

            value128 <<= 64;
            result = intType.Encode(value128).ToHex();
            intresult = intType.Decode<UInt128>(result);
            Assert.Equal(value128, intresult);
        }

        [Theory]
        [InlineData(sbyte.MaxValue)]
        [InlineData(sbyte.MaxValue / 3)]
        [InlineData(0)]
        [InlineData(sbyte.MinValue / 3)]
        [InlineData(sbyte.MinValue)]
        public virtual void ShouldEncodeDecodeSByte(sbyte value)
        {
            var intType = new IntType("int8");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<sbyte>(result);
            Assert.Equal(value, intresult);
        }

        [Theory]
        [InlineData(short.MaxValue)]
        [InlineData(short.MaxValue / 3)]
        [InlineData(0)]
        [InlineData(short.MinValue / 3)]
        [InlineData(short.MinValue)]
        public virtual void ShouldEncodeDecodeShort(short value)
        {
            var intType = new IntType("int16");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<short>(result);
            Assert.Equal(value, intresult);
        }

        [Theory]
        [InlineData(ulong.MaxValue)]
        [InlineData(1)]
        [InlineData(0)]
        public virtual void ShouldEncodeDecodeUInt64(ulong value)
        {
            var intType = new IntType("uint64");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<ulong>(result);
            Assert.Equal(value, intresult);
        }

        [Theory]
        [InlineData(ushort.MaxValue)]
        [InlineData(1)]
        [InlineData(0)]
        public virtual void ShouldEncodeDecodeUShort(ushort value)
        {
            var intType = new IntType("uint16");
            var result = intType.Encode(value).ToHex();
            var intresult = intType.Decode<ushort>(result);
            Assert.Equal(value, intresult);
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
        public virtual void ShouldThrowErrorWhenEncodingExceedingUintMaxValue()
        {
            var intType = new IntType("uint");
            var given = IntType.MAX_UINT256_VALUE + 1;
            var ex = Assert.Throws<ArgumentOutOfRangeException>("value", () => intType.Encode(given));
            Assert.StartsWith("Unsigned SmartContract integer must not exceed maximum value for uint256", ex.Message);
        }

        [Fact]
        public virtual void ShouldThrowErrorWhenEncodingIsLessThanUintMaxValue()
        {
            var intType = new IntType("uint");
            var given = IntType.MIN_UINT_VALUE - 1;
            var ex = Assert.Throws<ArgumentOutOfRangeException>("value", () => intType.Encode(given));
            Assert.StartsWith("Unsigned SmartContract integer must not be less than the minimum value of uint:",
                ex.Message);
        }

        [Fact]
        public virtual void ShouldThrowErrorWhenEncodingExceedingIntMaxValue()
        {
            var intType = new IntType("int");
            var given = IntType.MAX_INT256_VALUE + 1;
            var ex = Assert.Throws<ArgumentOutOfRangeException>("value", () => intType.Encode(given));
            Assert.StartsWith("Signed SmartContract integer must not exceed maximum value for int256", ex.Message);
        }

        [Theory]
        [InlineData(ulong.MaxValue, ulong.MinValue, "uint64")]
        [InlineData(long.MaxValue, long.MinValue, "int64")]
        [InlineData(uint.MaxValue, uint.MinValue, "uint32")]
        [InlineData(int.MaxValue, int.MinValue, "int32")]
        [InlineData(ushort.MaxValue, ushort.MinValue, "uint16")]
        [InlineData(short.MaxValue, short.MinValue, "int16")]
        [InlineData(byte.MaxValue, byte.MinValue, "uint8")]
        [InlineData(sbyte.MaxValue, sbyte.MinValue, "int8")]
        public virtual void ShouldThrowOverflowErrorWhenDecodingIntOutOfBoundaries(dynamic maxValue, dynamic minValue, string evmType)
        {
            var encodingIntType = new IntType("int256");
            var decodingIntType = new IntType(evmType);
            String result;

            BigInteger givenMax = maxValue;
            givenMax *= 3;

            result = encodingIntType.Encode(givenMax).ToHex();
            Assert.Throws<OverflowException>(() => decodingIntType.Decode(result, maxValue.GetType()));

            BigInteger givenMin = minValue;
            givenMin--;

            result = encodingIntType.Encode(givenMin).ToHex();
            Assert.Throws<OverflowException>(() => decodingIntType.Decode(result, minValue.GetType()));
        }

        [Fact]
        public virtual void ShouldThrowOverflowErrorWhenDecodingInt128OutOfBoundaries()
        {
            var encodingIntType = new IntType("int256");
            var decodingIntType = new IntType("int128");
            String result;

            BigInteger givenMax = Int128.MaxValue;
            givenMax *= 3;
            result = encodingIntType.Encode(givenMax).ToHex();
            Assert.Throws<OverflowException>(() => decodingIntType.Decode(result, typeof(Int128)));

            BigInteger givenMin = Int128.MinValue;
            givenMin--;
            result = encodingIntType.Encode(givenMin).ToHex();
            Assert.Throws<OverflowException>(() => decodingIntType.Decode(result, typeof(Int128)));
        }

        [Fact]
        public virtual void ShouldThrowErrorWhenEncodingIsLessThanIntMinValue()
        {
            var intType = new IntType("int");
            var given = IntType.MIN_INT256_VALUE - 1;
            var ex = Assert.Throws<ArgumentOutOfRangeException>("value", () => intType.Encode(given));
            Assert.StartsWith("Signed SmartContract integer must not be less than the minimum value for int256",
                ex.Message);
        }

        [Fact]
        public virtual void ShouldThrowErrorWhenValueIsNull()
        {
            var intType = new IntType("int");
            object given = null;
            var ex = Assert.Throws<Exception>(() => intType.Encode(given));
            Assert.Equal("Invalid value for type 'Nethereum.ABI.Encoders.IntTypeEncoder'. Value: null, ValueType: ()",
                ex.Message);
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