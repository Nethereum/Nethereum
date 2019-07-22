using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class HexBigIntegerTests
    {
        [Fact]
        public void ToStringReturnsBigIntegerToString()
        {
            Assert.Equal("1000", new HexBigInteger(1000).ToString());
            Assert.Equal("1000", $"{new HexBigInteger(1000)}");
            Assert.Equal("0", $"{new HexBigInteger("0x")}");
            Assert.Equal("0", $"{new HexBigInteger(null)}");
            Assert.Equal("1000", $"{new HexBigInteger("3E8")}");
        }

        [Fact]
        public void TwoHexBigIntegersWithTheSameValueAreEqual()
        {
            var val1 = new HexBigInteger(100);
            var val2 = new HexBigInteger(100);

            Assert.Equal(val1, val2);
            Assert.True(val1 == val2);
            Assert.True(val1.Equals(val2));
        }

        [Fact]
        public void TwoHexBigIntegersWithDifferingValuesAreNotEqual()
        {
            var val1 = new HexBigInteger(100);
            var val2 = new HexBigInteger(101);

            Assert.NotEqual(val1, val2);
        }

        [Fact]
        public void TwoNullHexBigIntegersAreEqual()
        {
            HexBigInteger val1 = null;
            HexBigInteger val2 = null;

            Assert.Equal(val1, val2);
        }

        [Fact]
        public void ANonNullHexBigIntegerIsNotEqualToANull()
        {
            var nonNull = new HexBigInteger(100);
            HexBigInteger nullHexBigInteger = null;

            Assert.NotEqual(nonNull, nullHexBigInteger);
            Assert.NotEqual(nullHexBigInteger, nonNull);
            Assert.False(nonNull == nullHexBigInteger);
            Assert.False(nullHexBigInteger == nonNull);
            Assert.True(nonNull != nullHexBigInteger);
            Assert.True(nullHexBigInteger != nonNull);
        }


    }
}
