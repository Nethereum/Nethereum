using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetScheduleIsTheOneStatementOfMainnetTests
    {
        private static IEnumerable<long> ActivationBlocks() =>
            typeof(MainnetChainActivations)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(long))
                .Select(f => (long)f.GetValue(null)!);

        private static IEnumerable<ulong> ActivationTimestamps()
        {
            foreach (var field in typeof(MainnetChainActivations).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(ulong)) yield return (ulong)field.GetValue(null)!;
                else if (field.FieldType == typeof(ulong?) && field.GetValue(null) is ulong scheduled) yield return scheduled;
            }
        }

        private static readonly ulong[] GethMainnetConfigBlocks =
        {
            1_150_000, 1_920_000, 2_463_000, 2_675_000, 4_370_000, 7_280_000, 7_280_000,
            9_069_000, 9_200_000, 12_244_000, 12_965_000, 13_773_000, 15_050_000
        };

        private static readonly ulong[] GethMainnetConfigTimestamps =
        {
            1_681_338_455, 1_710_338_135, 1_746_612_311, 1_764_798_551, 1_765_290_071, 1_767_747_671
        };

        [Fact]
        public void Given_TheMainnetSchedule_When_ItDerivesForkIdentityThresholds_Then_TheyAreGethsMainnetConfigListExactly()
        {
            var thresholds = MainnetChainSchedule.Instance.ForkThresholds();
            var blockHeights = thresholds.BlockHeights;
            var timestamps = thresholds.Timestamps;

            Assert.Equal(GethMainnetConfigBlocks, blockHeights);
            Assert.Equal(GethMainnetConfigTimestamps, timestamps);
        }

        [Fact]
        public void Given_TwoForksScheduledAtOneBlock_When_ThresholdsAreDerived_Then_ThatBlockIsListedOncePerFork()
        {
            var blockHeights = MainnetChainSchedule.Instance.ForkThresholds().BlockHeights;

            Assert.Equal(2, blockHeights.Count(b => b == (ulong)MainnetChainActivations.ConstantinopleBlock));
        }

        [Fact]
        public void Given_AForkThatDoesNotCountTowardIdentity_When_ThresholdsAreDerived_Then_ItIsAbsentWhileStillResolving()
        {
            var blockHeights = MainnetChainSchedule.Instance.ForkThresholds().BlockHeights;

            Assert.DoesNotContain((ulong)MainnetChainActivations.ParisBlock, blockHeights);
            Assert.DoesNotContain((ulong)MainnetChainActivations.FrontierThawingBlock, blockHeights);

            var activations = MainnetChainSchedule.Instance.ResolveActivations();
            Assert.Equal(HardforkName.Paris, activations.ResolveAt(MainnetChainActivations.ParisBlock, 0));
            Assert.Equal(HardforkName.FrontierThawing, activations.ResolveAt(MainnetChainActivations.FrontierThawingBlock, 0));
        }

        [Fact]
        public void Given_AForkMarkedNotToCountTowardIdentity_When_ThresholdsAreDerived_Then_TheDerivationActuallyReadsThatFlag()
        {
            var schedule = MainnetChainSchedule.Instance;
            schedule.Schedule.Single(a => a.Fork == HardforkName.Homestead.ToString()).CountsTowardForkIdentity = false;

            var blockHeights = schedule.ForkThresholds().BlockHeights;

            Assert.DoesNotContain((ulong)MainnetChainActivations.HomesteadBlock, blockHeights);
            Assert.NotEqual(GethMainnetConfigBlocks, blockHeights);
        }

        [Fact]
        public void Given_TheMainnetSchedule_When_AskedAtEveryActivationAndEitherSideOfIt_Then_ItResolvesWhatTheActivationsResolve()
        {
            var derived = MainnetChainSchedule.Instance.ResolveActivations();
            var declared = MainnetChainActivations.Instance;

            foreach (var block in ActivationBlocks())
                foreach (var at in new[] { block - 1, block, block + 1 })
                    Assert.Equal(declared.ResolveAt(at, 0), derived.ResolveAt(at, 0));

            foreach (var timestamp in ActivationTimestamps())
                foreach (var at in new[] { timestamp - 1, timestamp, timestamp + 1 })
                    Assert.Equal(
                        declared.ResolveAt(MainnetChainActivations.ParisBlock, at),
                        derived.ResolveAt(MainnetChainActivations.ParisBlock, at));
        }

        [Fact]
        public void Given_AScheduleMissingAFork_When_ComparedAtThatForksBoundary_Then_TheBoundarySweepSeesTheDifference()
        {
            var schedule = MainnetChainSchedule.Instance;
            schedule.Schedule.RemoveAll(a => a.Fork == HardforkName.Berlin.ToString());

            var withoutBerlin = schedule.ResolveActivations();

            Assert.NotEqual(
                MainnetChainActivations.Instance.ResolveAt(MainnetChainActivations.BerlinBlock, 0),
                withoutBerlin.ResolveAt(MainnetChainActivations.BerlinBlock, 0));
        }

        [Fact]
        public void Given_TheMainnetSchedule_When_AskedWhichChainItIs_Then_ItIsMainnet()
        {
            Assert.Equal(MainnetGenesisConstants.ChainId, MainnetChainSchedule.Instance.ChainId);
        }
    }
}
