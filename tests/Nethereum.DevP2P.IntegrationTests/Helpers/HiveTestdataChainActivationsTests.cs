using Nethereum.EVM;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public class HiveTestdataChainActivationsTests
    {
        private readonly IChainActivations _activations = HiveTestdataChainActivations.Instance;

        [Theory]
        [InlineData(0,   0,    HardforkName.Shanghai)]
        [InlineData(1,   0,    HardforkName.Shanghai)]
        [InlineData(100, 0,    HardforkName.Shanghai)]
        [InlineData(1,   59,   HardforkName.Shanghai)]
        [InlineData(1,   60,   HardforkName.Cancun)]
        [InlineData(200, 90,   HardforkName.Cancun)]
        [InlineData(300, 119,  HardforkName.Cancun)]
        [InlineData(1,   120,  HardforkName.Prague)]
        [InlineData(400, 3000, HardforkName.Prague)]
        [InlineData(600, 6000, HardforkName.Prague)]
        public void ResolveAt_HiveTestdataSchedule_ReturnsExpectedFork(long blockNumber, ulong timestamp, HardforkName expected)
        {
            Assert.Equal(expected, _activations.ResolveAt(blockNumber, timestamp));
        }
    }
}
