using System.Collections.Generic;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class ChainForkScheduleTests
    {
        private const ulong CancunOnMainnet = 1_710_338_135;

        [Fact]
        public void Given_AChainThatPinsOneFork_When_ItResolves_Then_EveryPointRunsThatFork()
        {
            var activations = ChainRules.ForConsensus(new ChainForkSchedule { ChainId = 31337, Hardfork = "amsterdam" });

            Assert.Equal(HardforkName.Amsterdam, activations.ResolveAt(0, 0));
            Assert.Equal(HardforkName.Amsterdam, activations.ResolveAt(1_000_000, CancunOnMainnet));
        }

        [Fact]
        public void Given_AChainStatingItsOwnSchedule_When_ItResolves_Then_TheForkChangesWhereTheScheduleSaid()
        {
            var activations = ChainRules.ForConsensus(new ChainForkSchedule
            {
                ChainId = 4242,
                GenesisFork = "london",
                Schedule = new List<ForkActivationEntry>
                {
                    new ForkActivationEntry { Fork = "cancun", Timestamp = 1_000 },
                    new ForkActivationEntry { Fork = "amsterdam", Timestamp = 2_000 }
                }
            });

            Assert.Equal(HardforkName.London, activations.ResolveAt(0, 999));
            Assert.Equal(HardforkName.Cancun, activations.ResolveAt(0, 1_000));
            Assert.Equal(HardforkName.Amsterdam, activations.ResolveAt(0, 2_000));
        }

        [Fact]
        public void Given_AKnownChainPreviewingAnUnscheduledFork_When_ItResolves_Then_ThePreviewApplies()
        {
            var activations = ChainRules.ForSimulation(
                new ChainForkSchedule { ChainId = 1 },
                new ForkPreview { Fork = "amsterdam", FromTimestamp = 2_000_000_000 });

            Assert.Equal(HardforkName.Amsterdam, activations.ResolveAt(30_000_000, 2_000_000_000));
        }

        [Fact]
        public void Given_AKnownChainPreviewingAnUnscheduledFork_When_AskedBeforeThePreview_Then_MainnetsOwnScheduleAnswers()
        {
            var previewing = ChainRules.ForSimulation(
                new ChainForkSchedule { ChainId = 1 },
                new ForkPreview { Fork = "amsterdam", FromTimestamp = 2_000_000_000 });

            Assert.Equal(
                MainnetChainActivations.Instance.ResolveAt(20_000_000, CancunOnMainnet),
                previewing.ResolveAt(20_000_000, CancunOnMainnet));
        }

        [Fact]
        public void Given_AKnownChainWithNothingStated_When_ItResolves_Then_TheChainsRegisteredScheduleAnswers()
        {
            var activations = ChainRules.ForConsensus(new ChainForkSchedule { ChainId = 1 });

            Assert.Equal(
                MainnetChainActivations.Instance.ResolveAt(20_000_000, CancunOnMainnet),
                activations.ResolveAt(20_000_000, CancunOnMainnet));
        }

        [Fact]
        public void Given_AnUndescribedChainWithNothingStated_When_ItResolves_Then_ItRefusesRatherThanGuessing()
        {
            Assert.ThrowsAny<System.InvalidOperationException>(
                () => ChainRules.ForConsensus(new ChainForkSchedule { ChainId = 987654 }, new ChainActivationsRegistry()));
        }
        [Fact]
        public void Given_APreviewStated_When_ConsensusAsks_Then_ItIsNotHonoured()
        {
            var chain = new ChainForkSchedule { ChainId = 1 };
            var preview = new ForkPreview { Fork = "amsterdam", FromBlock = 0 };

            var consensus = ChainRules.ForConsensus(chain);
            var simulation = ChainRules.ForSimulation(chain, preview);

            Assert.NotEqual(HardforkName.Amsterdam, consensus.ResolveAt(20_000_000, CancunOnMainnet));
            Assert.Equal(HardforkName.Amsterdam, simulation.ResolveAt(20_000_000, CancunOnMainnet));
        }

        [Fact]
        public void Given_AChainSchedule_When_ForkThresholdsAreDerived_Then_TheyComeFromTheSameStatement()
        {
            var chain = new ChainForkSchedule
            {
                ChainId = 4242,
                GenesisFork = "london",
                Schedule = new List<ForkActivationEntry>
                {
                    new ForkActivationEntry { Fork = "berlin", Block = 100 },
                    new ForkActivationEntry { Fork = "cancun", Timestamp = 1_000 },
                    new ForkActivationEntry { Fork = "amsterdam" }
                }
            };

            var thresholds = chain.ForkThresholds();

            Assert.Equal(new ulong[] { 100 }, thresholds.BlockHeights);
            Assert.Equal(new ulong[] { 1_000 }, thresholds.Timestamps);
        }

        [Fact]
        public void Given_ASingleForkChain_When_AskedForThresholds_Then_TheyAreEmptyBecauseItCrossesNoBoundary()
        {
            var chain = ChainForkSchedule.Running(420420, HardforkName.Amsterdam);

            var thresholds = chain.ForkThresholds();

            Assert.Empty(thresholds.BlockHeights);
            Assert.Empty(thresholds.Timestamps);
        }

        [Fact]
        public void Given_AScheduleResolvedByTheRegistry_When_AskedForThresholds_Then_ItDoesNotDisagreeWithHowItResolvesTheFork()
        {
            var chain = new ChainForkSchedule { ChainId = 987654321 };
            var registry = new ChainActivationsRegistry
            {
                DefaultForUnregisteredChains = ScheduledChainActivations.RunningOnly(HardforkName.Prague)
            };

            var thresholds = chain.ForkThresholds(registry);

            Assert.Empty(thresholds.BlockHeights);
            Assert.Empty(thresholds.Timestamps);
            Assert.Equal(HardforkName.Prague, chain.ResolveActivations(registry).ResolveAt(0, 0));
        }

        [Fact]
        public void Given_AKnownChainRegisteredWithItsSchedule_When_ResolvedAtAHistoricalBlock_Then_ItUsesTheScheduleNotADefault()
        {
            const long chainId = 424242;
            var schedule = new ChainForkSchedule
            {
                ChainId = chainId,
                GenesisFork = HardforkName.Prague.ToString(),
                Schedule = { new ForkActivationEntry { Fork = HardforkName.Amsterdam.ToString(), Timestamp = 1_000 } }
            };
            var registry = new ChainActivationsRegistry();
            registry.Register(chainId, schedule.ResolveActivations());

            Assert.Equal(HardforkName.Prague, registry.ResolveAt(chainId, 0, 999));
            Assert.Equal(HardforkName.Amsterdam, registry.ResolveAt(chainId, 0, 1_000));
        }
    }
}
