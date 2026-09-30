using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperShouldBalHealTests
    {
        private sealed class FixedActivations : IChainActivations
        {
            private readonly HardforkName _fork;
            public FixedActivations(HardforkName fork) => _fork = fork;
            public HardforkName ResolveAt(long blockNumber, ulong timestamp) => _fork;
        }

        private static BlockHeader Header(ulong blockNumber, byte[] balHash = null) => new BlockHeader
        {
            BlockNumber = new Nethereum.Util.EvmUInt256(blockNumber),
            StateRoot = new byte[32],
            BlockAccessListHash = balHash ?? new byte[32],
        };

        [Fact]
        public void ShouldBalHeal_DisabledByConfig_ReturnsFalse()
        {
            var activations = new FixedActivations(HardforkName.Amsterdam);
            Assert.False(SnapBootstrapper.ShouldBalHeal(activations, Header(10), balHealEnabled: false));
        }

        [Fact]
        public void ShouldBalHeal_EnabledButPreAmsterdam_ReturnsFalse()
        {
            var activations = new FixedActivations(HardforkName.Osaka);
            Assert.False(SnapBootstrapper.ShouldBalHeal(activations, Header(10), balHealEnabled: true));
        }

        [Fact]
        public void ShouldBalHeal_EnabledAndAmsterdamActive_ReturnsTrue()
        {
            var activations = new FixedActivations(HardforkName.Amsterdam);
            Assert.True(SnapBootstrapper.ShouldBalHeal(activations, Header(10), balHealEnabled: true));
        }

        [Fact]
        public void ShouldBalHeal_NullActivations_ReturnsFalse()
        {
            Assert.False(SnapBootstrapper.ShouldBalHeal(null, Header(10), balHealEnabled: true));
        }

        [Fact]
        public void ShouldBalHeal_NullHeader_ReturnsFalse()
        {
            var activations = new FixedActivations(HardforkName.Amsterdam);
            Assert.False(SnapBootstrapper.ShouldBalHeal(activations, null, balHealEnabled: true));
        }

    }
}
