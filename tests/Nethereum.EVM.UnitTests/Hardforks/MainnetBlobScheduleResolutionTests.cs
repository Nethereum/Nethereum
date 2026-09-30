using System.Linq;
using Nethereum.EVM.Hardforks;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class MainnetBlobScheduleResolutionTests
    {
        private static HardforkConfig ConfigFor(HardforkName fork) =>
            HardforkConfigFromSpec.Build(HardforkSpecRegistry.All.First(s => s.Name == fork));

        private static EvmUInt256 ReferenceFakeExponential(long factor, long numerator, long denominator)
        {
            var i = 1L;
            var output = System.Numerics.BigInteger.Zero;
            var numeratorAccum = (System.Numerics.BigInteger)factor * denominator;
            while (numeratorAccum != 0)
            {
                output += numeratorAccum;
                numeratorAccum = (numeratorAccum * numerator) / (denominator * i);
                i += 1;
            }
            return EvmUInt256BigIntegerExtensions.FromBigInteger(output / denominator);
        }

        private static void AssertEpoch(ulong timestamp, HardforkName expectedFork,
            int expectedFraction, int expectedMaxBlobsPerBlock)
        {
            var resolved = MainnetChainActivations.Instance.ResolveAt(blockNumber: 30_000_000, timestamp);
            Assert.Equal(expectedFork, resolved);

            var config = ConfigFor(resolved);
            Assert.Equal(expectedMaxBlobsPerBlock, config.MaxBlobsPerBlock);

            var excess = new EvmUInt256(50_000_000UL);
            var expectedFee = ReferenceFakeExponential(1, 50_000_000L, expectedFraction);
            var actualFee = config.IntrinsicGasRules.Blob!.CalculateBlobBaseFee(excess);
            Assert.Equal(expectedFee, actualFee);
        }

        [Fact]
        public void Timestamp_inside_real_Osaka_epoch_resolves_to_Osakas_own_schedule()
        {
            AssertEpoch(timestamp: 1_765_000_000UL,
                expectedFork: HardforkName.Osaka,
                expectedFraction: 5_007_716,
                expectedMaxBlobsPerBlock: 9);
        }

        [Fact]
        public void Timestamp_inside_real_BPO1_epoch_resolves_to_BPO1s_schedule()
        {
            AssertEpoch(timestamp: 1_766_000_000UL,
                expectedFork: HardforkName.OsakaBpo1,
                expectedFraction: 8_346_193,
                expectedMaxBlobsPerBlock: 15);
        }

        [Fact]
        public void Timestamp_inside_real_BPO2_epoch_resolves_to_BPO2s_schedule()
        {
            AssertEpoch(timestamp: 1_768_000_000UL,
                expectedFork: HardforkName.OsakaBpo2,
                expectedFraction: 11_684_671,
                expectedMaxBlobsPerBlock: 21);
        }

        [Theory]
        [InlineData(1_764_798_550UL, HardforkName.Prague)]
        [InlineData(1_764_798_551UL, HardforkName.Osaka)]
        [InlineData(1_765_290_070UL, HardforkName.Osaka)]
        [InlineData(1_765_290_071UL, HardforkName.OsakaBpo1)]
        [InlineData(1_767_747_670UL, HardforkName.OsakaBpo1)]
        [InlineData(1_767_747_671UL, HardforkName.OsakaBpo2)]
        public void Boundary_timestamps_resolve_to_the_correct_side_of_each_activation(
            ulong timestamp, HardforkName expectedFork)
        {
            var resolved = MainnetChainActivations.Instance.ResolveAt(blockNumber: 30_000_000, timestamp);
            Assert.Equal(expectedFork, resolved);
        }

        [Fact]
        public void Mainnet_has_exactly_three_post_Prague_blob_boundaries_not_more()
        {
            Assert.NotNull(MainnetChainActivations.OsakaTimestamp);
            Assert.NotNull(MainnetChainActivations.OsakaBpo1Timestamp);
            Assert.NotNull(MainnetChainActivations.OsakaBpo2Timestamp);

            Assert.Equal(1_764_798_551UL, MainnetChainActivations.OsakaTimestamp);
            Assert.Equal(1_765_290_071UL, MainnetChainActivations.OsakaBpo1Timestamp);
            Assert.Equal(1_767_747_671UL, MainnetChainActivations.OsakaBpo2Timestamp);
        }
    }
}
