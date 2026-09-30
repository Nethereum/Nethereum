using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.MainnetChain;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class ForkConfigurationWiredBackendsTests
    {
        private static ForkConfiguration For(HardforkName fork) =>
            ForkConfiguration.For(fork, MainnetChainHardforkRegistry.Instance.Get(fork));

        [Fact]
        public void Given_TheKzgAndBlsAwareRegistry_When_ProjectingCancun_Then_KzgIsAvailableBecauseItsBackendIsWired()
        {
            var config = For(HardforkName.Cancun);

            Assert.Equal("0x000000000000000000000000000000000000000a", config.Precompiles["KZG_POINT_EVALUATION"]);
        }

        [Fact]
        public void Given_TheKzgAndBlsAwareRegistry_When_ProjectingAmsterdam_Then_TheBlsPrecompilesAreAvailable()
        {
            var config = For(HardforkName.Amsterdam);

            Assert.Equal("0x000000000000000000000000000000000000000b", config.Precompiles["BLS12_G1ADD"]);
            Assert.Equal("0x000000000000000000000000000000000000000f", config.Precompiles["BLS12_PAIRING_CHECK"]);
            Assert.Equal("0x000000000000000000000000000000000000000a", config.Precompiles["KZG_POINT_EVALUATION"]);
        }
    }
}
