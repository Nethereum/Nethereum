using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class AScheduledForkReachesTheExecutorTests
    {
        private static ChainConfig ChainRunning(string hardfork, IChainActivations activations = null) =>
            new ChainConfig { Hardfork = hardfork, Activations = activations };

        private static IChainActivations CancunUntil(ulong pragueFrom) =>
            new ScheduledChainActivations(HardforkName.Cancun, new[]
            {
                ForkActivation.AtTimestamp(HardforkName.Prague, pragueFrom)
            });

        [Fact]
        public void Given_AChainWithNoSchedule_When_ItResolvesActivations_Then_EveryPointIsTheForkItPins()
        {
            var activations = ChainRunning("osaka").ResolveActivations();

            Assert.Equal(HardforkName.Osaka, activations.ResolveAt(0, 0));
            Assert.Equal(HardforkName.Osaka, activations.ResolveAt(long.MaxValue, ulong.MaxValue));
        }

        [Fact]
        public void Given_AChainWithAnInjectedSchedule_When_ItResolvesActivations_Then_PinnedForkFollowsTheScheduleNotAStaleForkName()
        {
            var config = ChainRunning("osaka", CancunUntil(pragueFrom: 1_000));

            var activations = config.ResolveActivations();

            Assert.Equal(HardforkName.Cancun, activations.ResolveAt(0, 999));
            Assert.Equal(HardforkName.Prague, activations.ResolveAt(0, 1_000));
            Assert.Equal(HardforkName.Cancun, config.PinnedFork);
        }

        [Fact]
        public void Given_TheForkAtABlock_When_TheExecutorAsksForItsRules_Then_ItGetsThatForksRulesNotThePinnedForks()
        {
            var config = ChainRunning("osaka", CancunUntil(pragueFrom: 1_000));

            var atCancun = config.ConfigForFork(config.ResolveActivations().ResolveAt(0, 999));
            var atPrague = config.ConfigForFork(config.ResolveActivations().ResolveAt(0, 1_000));

            Assert.NotSame(atCancun, atPrague);
            Assert.Same(config.Registry.Get(HardforkName.Cancun), atCancun);
            Assert.Same(config.Registry.Get(HardforkName.Prague), atPrague);
        }

        [Fact]
        public void Given_AConfigThatPinsOneFork_When_TheExecutorAsksForEachForksRules_Then_ItStillGetsEachForksOwn()
        {
            var config = ChainRunning("prague");

            Assert.Same(config.Registry.Get(HardforkName.Cancun), config.ConfigForFork(HardforkName.Cancun));
            Assert.Same(config.Registry.Get(HardforkName.Amsterdam), config.ConfigForFork(HardforkName.Amsterdam));
        }

        [Fact]
        public void Given_AProducerAndAFollowerOnOneConfig_When_EachResolvesTheFork_Then_TheyCannotDisagree()
        {
            var config = ChainRunning("amsterdam", CancunUntil(pragueFrom: 1_000));

            var producer = config.ResolveActivations();
            var follower = config.ResolveActivations();

            foreach (var timestamp in new ulong[] { 0, 999, 1_000, 1_001, 2_000_000_000 })
                Assert.Equal(producer.ResolveAt(0, timestamp), follower.ResolveAt(0, timestamp));
        }

        [Fact]
        public void Given_AComponentPinsTheGenesisForkInsteadOfResolving_When_TheChainCrossesTheBoundary_Then_TheDisagreementIsVisible()
        {
            var config = ChainRunning("osaka", CancunUntil(pragueFrom: 1_000));

            var pinningGenesisFork = new FixedChainActivations(config.PinnedFork);

            Assert.Equal(config.ResolveActivations().ResolveAt(0, 999), pinningGenesisFork.ResolveAt(0, 999));
            Assert.NotEqual(
                config.ResolveActivations().ResolveAt(0, 1_000),
                pinningGenesisFork.ResolveAt(0, 1_000));
        }
    }
}
