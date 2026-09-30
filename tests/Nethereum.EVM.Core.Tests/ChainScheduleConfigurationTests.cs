using System;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class ChainScheduleConfigurationTests
    {
        private const long AfterTheMerge = 20_000_000;
        private const ulong CancunOnMainnet = 1_710_338_135;
        private const ulong BeforeCancun = CancunOnMainnet - 1;

        private static ScheduledChainActivations ACustomChain() =>
            new ScheduledChainActivations(HardforkName.Frontier, new[]
            {
                ForkActivation.AtBlock(HardforkName.Berlin, 100),
                ForkActivation.AtBlock(HardforkName.London, 200),
                ForkActivation.AtTimestamp(HardforkName.Cancun, 1_000),
                ForkActivation.AtTimestamp(HardforkName.Prague, 2_000),
                ForkActivation.Unscheduled(HardforkName.Amsterdam)
            });

        [Fact]
        public void Given_ACustomChain_When_AskedBeforeItsFirstFork_Then_ItRunsTheForkItStartedAt()
        {
            Assert.Equal(HardforkName.Frontier, ACustomChain().ResolveAt(0, 0));
        }

        [Theory]
        [InlineData(99, 0, HardforkName.Frontier)]
        [InlineData(100, 0, HardforkName.Berlin)]
        [InlineData(199, 0, HardforkName.Berlin)]
        [InlineData(200, 0, HardforkName.London)]
        public void Given_ACustomChain_When_AskedAcrossABlockBoundary_Then_TheForkChangesOnTheBlockItNames(
            long blockNumber, ulong timestamp, HardforkName expected)
        {
            Assert.Equal(expected, ACustomChain().ResolveAt(blockNumber, timestamp));
        }

        [Theory]
        [InlineData(999, HardforkName.London)]
        [InlineData(1_000, HardforkName.Cancun)]
        [InlineData(1_999, HardforkName.Cancun)]
        [InlineData(2_000, HardforkName.Prague)]
        public void Given_ACustomChain_When_AskedAcrossATimestampBoundary_Then_TheForkChangesOnTheTimestampItNames(
            ulong timestamp, HardforkName expected)
        {
            Assert.Equal(expected, ACustomChain().ResolveAt(300, timestamp));
        }

        [Fact]
        public void Given_AForkTheChainHasNotScheduled_When_TheChainIsAsked_Then_ItIsNeverReturned()
        {
            var chain = ACustomChain();

            Assert.NotEqual(HardforkName.Amsterdam, chain.ResolveAt(long.MaxValue, ulong.MaxValue));
            Assert.DoesNotContain(chain.Scheduled, a => a.Fork == HardforkName.Amsterdam);
        }

        [Fact]
        public void Given_AnUnscheduledForkBroughtForward_When_TheChainIsAsked_Then_ItRunsFromThePointNamed()
        {
            var previewing = new PreviewingChainActivations(
                ACustomChain(), ForkActivation.AtTimestamp(HardforkName.Amsterdam, 3_000));

            Assert.Equal(HardforkName.Prague, previewing.ResolveAt(300, 2_999));
            Assert.Equal(HardforkName.Amsterdam, previewing.ResolveAt(300, 3_000));
        }

        [Fact]
        public void Given_APreviewNotYetReached_When_TheChainIsAsked_Then_ItAnswersExactlyAsTheChainWould()
        {
            var chain = ACustomChain();
            var previewing = new PreviewingChainActivations(
                chain, ForkActivation.AtTimestamp(HardforkName.Amsterdam, 9_999));

            foreach (var point in new[] { (0L, 0UL), (150L, 0UL), (300L, 1_000UL), (300L, 2_000UL) })
                Assert.Equal(chain.ResolveAt(point.Item1, point.Item2), previewing.ResolveAt(point.Item1, point.Item2));
        }

        [Fact]
        public void Given_MainnetPreviewingAmsterdam_When_AskedBeforeThePreview_Then_MainnetsOwnScheduleStillAnswers()
        {
            var previewing = new PreviewingChainActivations(
                MainnetChainActivations.Instance, ForkActivation.AtTimestamp(HardforkName.Amsterdam, ulong.MaxValue));

            Assert.Equal(
                MainnetChainActivations.Instance.ResolveAt(AfterTheMerge, BeforeCancun),
                previewing.ResolveAt(AfterTheMerge, BeforeCancun));
        }

        [Fact]
        public void Given_APreviewWithNoPointToTakeEffectAt_When_Built_Then_ItIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new PreviewingChainActivations(
                MainnetChainActivations.Instance, ForkActivation.Unscheduled(HardforkName.Amsterdam)));
        }

        [Fact]
        public void Given_AChainNobodyHasDescribed_When_NoDefaultIsStated_Then_AskingIsAnError()
        {
            var registry = new ChainActivationsRegistry();

            Assert.Throws<InvalidOperationException>(() => registry.Get(31337));
        }

        [Fact]
        public void Given_AChainNobodyHasDescribed_When_ADefaultIsStated_Then_ItAnswersWithIt()
        {
            var registry = new ChainActivationsRegistry
            {
                DefaultForUnregisteredChains = ScheduledChainActivations.RunningOnly(HardforkName.Amsterdam)
            };

            Assert.Equal(HardforkName.Amsterdam, registry.ResolveAt(31337, 0, 0));
        }

        [Fact]
        public void Given_ADescribedChain_When_ADefaultIsAlsoStated_Then_TheChainsOwnScheduleWins()
        {
            var registry = new ChainActivationsRegistry
            {
                DefaultForUnregisteredChains = ScheduledChainActivations.RunningOnly(HardforkName.Amsterdam)
            };

            Assert.Equal(
                MainnetChainActivations.Instance.ResolveAt(AfterTheMerge, BeforeCancun),
                registry.ResolveAt(1, AfterTheMerge, BeforeCancun));
        }
    }
}
