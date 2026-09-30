using System;
using System.Numerics;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class BigIntegerExtensionsTests
    {
        [Fact]
        public void ToByteArrayUnsignedBigEndian_Zero_ReturnsSingleZeroByte()
        {
            var result = BigInteger.Zero.ToByteArrayUnsignedBigEndian();
            Assert.Single(result);
            Assert.Equal(0, result[0]);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_One_ReturnsSingleOneByte()
        {
            var result = BigInteger.One.ToByteArrayUnsignedBigEndian();
            Assert.Single(result);
            Assert.Equal(1, result[0]);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_255_ReturnsSingleByte()
        {
            var result = new BigInteger(255).ToByteArrayUnsignedBigEndian();
            Assert.Single(result);
            Assert.Equal(0xFF, result[0]);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_256_ReturnsTwoBytes()
        {
            var result = new BigInteger(256).ToByteArrayUnsignedBigEndian();
            Assert.Equal(2, result.Length);
            Assert.Equal(0x01, result[0]);
            Assert.Equal(0x00, result[1]);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_128_StripsSignByte()
        {
            // BigInteger(128).ToByteArray() returns [0x80, 0x00] (sign byte needed)
            // Our method should strip the sign byte and return [0x80]
            var result = new BigInteger(128).ToByteArrayUnsignedBigEndian();
            Assert.Single(result);
            Assert.Equal(0x80, result[0]);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_LargeValue_CorrectBigEndianOrder()
        {
            // 0x0102030405
            var value = new BigInteger(0x0102030405L);
            var result = value.ToByteArrayUnsignedBigEndian();
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, result);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_1Ether_CorrectEncoding()
        {
            // 1 ETH = 10^18 = 0x0DE0B6B3A7640000
            var oneEther = BigInteger.Parse("1000000000000000000");
            var result = oneEther.ToByteArrayUnsignedBigEndian();
            Assert.Equal(new byte[] { 0x0D, 0xE0, 0xB6, 0xB3, 0xA7, 0x64, 0x00, 0x00 }, result);
        }

        [Fact]
        public void ToByteArrayUnsignedBigEndian_NegativeValue_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new BigInteger(-1).ToByteArrayUnsignedBigEndian());
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_Null_ReturnsZero()
        {
            Assert.Equal(BigInteger.Zero, ((byte[])null).ToBigIntegerFromUnsignedBigEndian());
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_Empty_ReturnsZero()
        {
            Assert.Equal(BigInteger.Zero, new byte[0].ToBigIntegerFromUnsignedBigEndian());
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_SingleZero_ReturnsZero()
        {
            Assert.Equal(BigInteger.Zero, new byte[] { 0x00 }.ToBigIntegerFromUnsignedBigEndian());
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_SingleByte_ReturnsCorrectValue()
        {
            Assert.Equal(new BigInteger(255), new byte[] { 0xFF }.ToBigIntegerFromUnsignedBigEndian());
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_HighBitSet_TreatedAsUnsigned()
        {
            // 0x80 = 128 unsigned, not -128
            var result = new byte[] { 0x80 }.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(new BigInteger(128), result);
            Assert.True(result.Sign > 0);
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_AllFF_TreatedAsUnsigned()
        {
            // 0xFFFF = 65535 unsigned, not -1
            var result = new byte[] { 0xFF, 0xFF }.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(new BigInteger(65535), result);
            Assert.True(result.Sign > 0);
        }

        [Fact]
        public void ToBigIntegerFromUnsignedBigEndian_1Ether_CorrectDecoding()
        {
            var bytes = new byte[] { 0x0D, 0xE0, 0xB6, 0xB3, 0xA7, 0x64, 0x00, 0x00 };
            var result = bytes.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(BigInteger.Parse("1000000000000000000"), result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(65535)]
        [InlineData(16777215)]
        [InlineData(int.MaxValue)]
        [InlineData(long.MaxValue)]
        public void Roundtrip_ToBytesThenBack_PreservesValue(long input)
        {
            var original = new BigInteger(input);
            var bytes = original.ToByteArrayUnsignedBigEndian();
            var restored = bytes.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(original, restored);
        }

        [Fact]
        public void Roundtrip_LargeValue_PreservesValue()
        {
            // 2^256 - 1 (max uint256)
            var maxUint256 = BigInteger.Pow(2, 256) - 1;
            var bytes = maxUint256.ToByteArrayUnsignedBigEndian();
            var restored = bytes.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(maxUint256, restored);
            Assert.Equal(32, bytes.Length);
        }

        [Fact]
        public void Roundtrip_PowerOf2Boundary_PreservesValue()
        {
            // 2^128 — boundary where sign byte matters
            var value = BigInteger.Pow(2, 128);
            var bytes = value.ToByteArrayUnsignedBigEndian();
            var restored = bytes.ToBigIntegerFromUnsignedBigEndian();
            Assert.Equal(value, restored);
        }
    }
}
