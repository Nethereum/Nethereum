using Nethereum.CoreChain.Freezer;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class ChainBlobBaseFeeFractionResolverTests
    {
        private static BlockHeader MainnetHeaderAt(ulong timestamp) =>
            new BlockHeader { BlockNumber = 24_000_000, Timestamp = (long)timestamp };

        [Theory]
        [InlineData(MainnetChainActivations.CancunTimestamp, 3_338_477)]
        [InlineData(MainnetChainActivations.PragueTimestamp, 5_007_716)]
        [InlineData(MainnetBlobSchedule.Bpo1Timestamp, 8_346_193)]
        [InlineData(MainnetBlobSchedule.Bpo2Timestamp, 11_684_671)]
        public void Given_AMainnetHeader_When_ItsBlobFeeFractionIsResolved_Then_ItIsTheFractionActiveAtItsTimestamp(
            ulong timestamp, int expectedFraction)
        {
            var fraction = ChainBlobBaseFeeFractionResolver.Mainnet.FractionForBlock(MainnetHeaderAt(timestamp));

            Assert.Equal(new EvmUInt256((ulong)expectedFraction), fraction);
        }
    }
}
