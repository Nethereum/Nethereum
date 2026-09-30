using System;
using System.Linq;
using System.Reflection;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class DefaultConfigsAgreeWithTheRegistryTests
    {
        private const int Blake2fPrecompile = 9;

        private static HardforkConfig FromTable(HardforkName fork) =>
            (HardforkConfig)typeof(DefaultHardforkConfigs)
                .GetProperty(fork.ToString(), BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

        [Theory]
        [InlineData(HardforkName.Byzantium)]
        [InlineData(HardforkName.Constantinople)]
        [InlineData(HardforkName.Petersburg)]
        public void Given_AForkBeforeEip152_When_ItsPrecompilesAreRead_Then_Blake2fIsNotAmongThem(HardforkName fork)
        {
            Assert.DoesNotContain(Blake2fPrecompile, FromTable(fork).Precompiles.GetAddresses());
        }

        [Theory]
        [InlineData(HardforkName.Istanbul)]
        [InlineData(HardforkName.Berlin)]
        [InlineData(HardforkName.Osaka)]
        public void Given_AForkFromEip152Onwards_When_ItsPrecompilesAreRead_Then_Blake2fIsAmongThem(HardforkName fork)
        {
            Assert.Contains(Blake2fPrecompile, FromTable(fork).Precompiles.GetAddresses());
        }

        [Fact]
        public void Given_TheEngineKnowsAFork_When_NoNamedPresetExistsForIt_Then_ItIsStillReachableByAsking()
        {
            var newest = Enum.GetValues(typeof(HardforkName)).Cast<HardforkName>()
                .Where(f => f != HardforkName.Unspecified)
                .Max();

            Assert.Equal(HardforkName.Amsterdam, newest);
            Assert.Null(FromTable(newest));
            Assert.NotNull(DefaultHardforkConfigs.For(newest));
        }
    }
}
