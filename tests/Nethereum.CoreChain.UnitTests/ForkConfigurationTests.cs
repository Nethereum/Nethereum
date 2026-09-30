using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ForkConfigurationTests
    {
        private static ForkConfiguration For(HardforkName fork) =>
            ForkConfiguration.For(fork, DefaultMainnetHardforkRegistry.Instance.Get(fork));

        [Fact]
        public void Given_TheManagedRegistry_When_ProjectingCancun_Then_ItNamesTheWiredPrecompilesButNotThePlaceholderKzg()
        {
            var config = For(HardforkName.Cancun);

            Assert.Equal("0x0000000000000000000000000000000000000001", config.Precompiles["ECREC"]);
            Assert.Equal("0x0000000000000000000000000000000000000008", config.Precompiles["BN254_PAIRING"]);
            Assert.False(config.Precompiles.ContainsKey("KZG_POINT_EVALUATION"),
                "KZG is a placeholder on the managed registry - no native backend loaded, so it is not available to call");
            Assert.True(config.SystemContracts.ContainsKey("BEACON_ROOTS_ADDRESS"));
            Assert.False(config.SystemContracts.ContainsKey("BUILDER_DEPOSIT_CONTRACT_ADDRESS"));
        }

        [Fact]
        public void Given_TheManagedRegistry_When_ProjectingAmsterdam_Then_P256IsWiredButKzgAndBlsAreNot()
        {
            var config = For(HardforkName.Amsterdam);

            Assert.Equal("0x0000000000000000000000000000000000000100", config.Precompiles["P256VERIFY"]);
            Assert.False(config.Precompiles.ContainsKey("KZG_POINT_EVALUATION"));
            Assert.False(config.Precompiles.ContainsKey("BLS12_PAIRING_CHECK"));
            Assert.True(config.SystemContracts.ContainsKey("BUILDER_DEPOSIT_CONTRACT_ADDRESS"));
            Assert.True(config.SystemContracts.ContainsKey("BUILDER_EXIT_CONTRACT_ADDRESS"));
            Assert.True(config.SystemContracts.ContainsKey("HISTORY_STORAGE_ADDRESS"));
        }

        [Fact]
        public void Given_Byzantium_When_Projected_Then_Blake2fIsNotNamedBecauseItIsNotDispatchedThere()
        {
            var config = For(HardforkName.Byzantium);

            Assert.False(config.Precompiles.ContainsKey("BLAKE2F"));
            Assert.True(config.Precompiles.ContainsKey("BN254_PAIRING"));
        }

        [Fact]
        public void Given_TheCreate2Factory_When_Projected_Then_ItIsNotNamedBecauseEthConfigDoesNotEmitIt()
        {
            var config = For(HardforkName.Amsterdam);

            Assert.DoesNotContain(config.SystemContracts.Values,
                v => v.ToLowerInvariant() == "0x4e59b44847b379578588920ca78fbf26c0b4956c");
        }
    }
}
