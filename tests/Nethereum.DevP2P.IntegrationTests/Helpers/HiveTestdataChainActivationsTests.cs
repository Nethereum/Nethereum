using Nethereum.EVM;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Unit tests for <see cref="HiveTestdataChainActivations"/>. The schedule
    /// comes from go-ethereum's <c>cmd/devp2p/internal/ethtest/testdata/forkenv.json</c>;
    /// pinning each block-range / timestamp-range to its expected fork is the
    /// prerequisite for replaying the chain block-by-block against canonical
    /// state roots.
    /// </summary>
    public class HiveTestdataChainActivationsTests
    {
        private readonly IChainActivations _activations = HiveTestdataChainActivations.Instance;

        [Theory]
        [InlineData(0,   0,    HardforkName.Homestead)]
        [InlineData(5,   0,    HardforkName.Homestead)]
        [InlineData(6,   0,    HardforkName.TangerineWhistle)]
        [InlineData(11,  0,    HardforkName.TangerineWhistle)]
        [InlineData(12,  0,    HardforkName.SpuriousDragon)]
        [InlineData(17,  0,    HardforkName.SpuriousDragon)]
        [InlineData(18,  0,    HardforkName.Byzantium)]
        [InlineData(23,  0,    HardforkName.Byzantium)]
        [InlineData(24,  0,    HardforkName.Constantinople)]
        [InlineData(29,  0,    HardforkName.Constantinople)]
        [InlineData(30,  0,    HardforkName.Petersburg)]
        [InlineData(35,  0,    HardforkName.Petersburg)]
        [InlineData(36,  0,    HardforkName.Istanbul)]
        [InlineData(41,  0,    HardforkName.Istanbul)]
        [InlineData(42,  0,    HardforkName.MuirGlacier)]
        [InlineData(47,  0,    HardforkName.MuirGlacier)]
        [InlineData(48,  0,    HardforkName.Berlin)]
        [InlineData(53,  0,    HardforkName.Berlin)]
        [InlineData(54,  0,    HardforkName.London)]
        [InlineData(59,  0,    HardforkName.London)]
        [InlineData(60,  0,    HardforkName.ArrowGlacier)]
        [InlineData(65,  0,    HardforkName.ArrowGlacier)]
        [InlineData(66,  0,    HardforkName.GrayGlacier)]
        [InlineData(71,  0,    HardforkName.GrayGlacier)]
        [InlineData(72,  0,    HardforkName.Paris)]
        [InlineData(72,  779,  HardforkName.Paris)]
        [InlineData(72,  780,  HardforkName.Shanghai)]
        [InlineData(100, 800,  HardforkName.Shanghai)]
        [InlineData(100, 839,  HardforkName.Shanghai)]
        [InlineData(100, 840,  HardforkName.Cancun)]
        [InlineData(500, 5000, HardforkName.Cancun)]
        public void ResolveAt_HiveTestdataSchedule_ReturnsExpectedFork(long blockNumber, ulong timestamp, HardforkName expected)
        {
            Assert.Equal(expected, _activations.ResolveAt(blockNumber, timestamp));
        }
    }
}
