using Nethereum.Hex.HexConvertors;
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
    }
}
