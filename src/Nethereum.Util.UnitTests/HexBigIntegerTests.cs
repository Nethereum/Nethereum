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
        }
    }
}
