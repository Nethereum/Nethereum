using Nethereum.Hex.HexTypes;
using System;
using System.Numerics;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    public class BlockRangeTest
    {
        [Fact]
        public void When_From_And_To_Are_The_Same_The_Range_Is_Equal()
        {
            Assert.Equal(new BlockRange(0, 1), new BlockRange(0, 1));
        }

        [Fact]
        public void When_From_And_To_Are_The_Same_The_Range_Is_Not_Equal()
        {
            Assert.NotEqual(new BlockRange(0, 1), new BlockRange(1, 0));
            Assert.NotEqual(new BlockRange(1, 0), new BlockRange(0, 1));
        }

        [Fact]
        public void From_Can_Not_Be_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new BlockRange(null, new HexBigInteger(BigInteger.One)));
        }

        [Fact]
        public void To_Can_Not_Be_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new BlockRange(new HexBigInteger(BigInteger.One), null));
        }

    }
}
