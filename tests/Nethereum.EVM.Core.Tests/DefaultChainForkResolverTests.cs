using System;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class DefaultChainForkResolverTests
    {
        [Fact]
        public void Given_TheDefault_When_TheEngineLearnsANewerFork_Then_TheDefaultIsThatForkWithNoEditHere()
        {
            var newest = Enum.GetValues(typeof(HardforkName)).Cast<HardforkName>()
                .Where(f => f != HardforkName.Unspecified)
                .Max();

            Assert.Equal(newest, DefaultChainForkResolver.NewestImplementedFork);
        }

        [Fact]
        public void Given_ThisReleasesEngine_When_TheDefaultIsAsked_Then_ItIsAmsterdam()
        {
            Assert.Equal(HardforkName.Amsterdam, DefaultChainForkResolver.NewestImplementedFork);
        }

        [Fact]
        public void Given_AnUndescribedChain_When_TheDefaultResolverPricesIt_Then_ItUsesTheNewestImplementedForksRules()
        {
            var resolver = DefaultChainForkResolver.Default;

            var config = resolver.ResolveConfigAt(chainId: 987654321, blockNumber: 0, timestamp: 0);

            Assert.Same(
                DefaultMainnetHardforkRegistry.Instance.Get(DefaultChainForkResolver.NewestImplementedFork),
                config);
        }

        [Fact]
        public void Given_TheDefault_When_ComparedToAStatedOsakaAssumption_Then_TheyDifferSoTheDefaultIsNotSilentlyOsaka()
        {
            var newest = DefaultChainForkResolver.Default.ResolveForkAt(987654321, 0, 0);
            var osaka = DefaultChainForkResolver.AssumingUndescribedChainsRun(HardforkName.Osaka)
                .ResolveForkAt(987654321, 0, 0);

            Assert.Equal(HardforkName.Amsterdam, newest);
            Assert.NotEqual(osaka, newest);
        }
    }
}
