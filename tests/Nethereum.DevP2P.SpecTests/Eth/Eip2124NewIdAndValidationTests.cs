using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.EVM.ForkId;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    /// <summary>
    /// AMS-2124-01: <see cref="Eip2124ForkIdCalculator.NewId"/> (the "what forkHash/forkNext do we
    /// advertise at this head" computation) and <see cref="Eip2124ForkIdCalculator.ValidateForkId"/>
    /// (the peer-acceptance ruleset). The known-answer oracle is go-ethereum's OWN mainnet test
    /// vectors - core/forkid/forkid_test.go, TestCreation's first table (mainnet) - reproduced here
    /// as literals, so a bug in the production schedule cannot also corrupt the oracle it is
    /// checked against.
    /// <para>
    /// EIP-2124 hashes a chain's CONFIG THRESHOLDS: the block or timestamp a fork activates at, not
    /// the timestamp of whichever block first reached it. For mainnet the two coincide, and the
    /// post-BPO2 known answer below is computed from
    /// <see cref="MainnetChainSchedule.ForkIdentityThresholds"/> to prove it.
    /// See memory <c>fork-id-config-thresholds-vs-observed-timestamps</c>.
    /// </para>
    /// </summary>
    public class Eip2124NewIdAndValidationTests
    {
        private static readonly byte[] MainnetGenesis =
            "d4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3".HexToByteArray();

        private static readonly ulong[] GethMainnetBlocks =
        {
            1_150_000, 1_920_000, 2_463_000, 2_675_000, 4_370_000, 7_280_000,
            9_069_000, 9_200_000, 12_244_000, 12_965_000, 13_773_000, 15_050_000
        };

        private static readonly ulong[] GethMainnetTimestamps =
        {
            1_681_338_455,
            1_710_338_135,
            1_746_612_311,
            1_764_798_551,
            1_765_290_071,
            1_767_747_671
        };

        [Theory]
        [InlineData(0UL, 0UL, 0xfc64ec04u, 1_150_000UL)]
        [InlineData(1_149_999UL, 0UL, 0xfc64ec04u, 1_150_000UL)]
        [InlineData(1_150_000UL, 0UL, 0x97c2c34cu, 1_920_000UL)]
        [InlineData(1_920_000UL, 0UL, 0x91d1f948u, 2_463_000UL)]
        [InlineData(2_463_000UL, 0UL, 0x7a64da13u, 2_675_000UL)]
        [InlineData(2_675_000UL, 0UL, 0x3edd5b10u, 4_370_000UL)]
        [InlineData(4_370_000UL, 0UL, 0xa00bc324u, 7_280_000UL)]
        [InlineData(7_280_000UL, 0UL, 0x668db0afu, 9_069_000UL)]
        [InlineData(9_069_000UL, 0UL, 0x879d6e30u, 9_200_000UL)]
        [InlineData(9_200_000UL, 0UL, 0xe029e991u, 12_244_000UL)]
        [InlineData(12_244_000UL, 0UL, 0x0eb440f6u, 12_965_000UL)]
        [InlineData(12_965_000UL, 0UL, 0xb715077du, 13_773_000UL)]
        [InlineData(13_773_000UL, 0UL, 0x20c327fcu, 15_050_000UL)]
        [InlineData(15_050_000UL, 0UL, 0xf0afd0e3u, 1_681_338_455UL)]
        [InlineData(20_000_000UL, 1_681_338_454UL, 0xf0afd0e3u, 1_681_338_455UL)]
        [InlineData(20_000_000UL, 1_681_338_455UL, 0xdce96c2du, 1_710_338_135UL)]
        [InlineData(30_000_000UL, 1_710_338_134UL, 0xdce96c2du, 1_710_338_135UL)]
        [InlineData(30_000_000UL, 1_710_338_135UL, 0x9f3d2254u, 1_746_612_311UL)]
        [InlineData(30_000_000UL, 1_746_612_311UL, 0xc376cf8bu, 1_764_798_551UL)]
        [InlineData(30_000_000UL, 1_764_798_550UL, 0xc376cf8bu, 1_764_798_551UL)]
        [InlineData(30_000_000UL, 1_764_798_551UL, 0x5167e2a6u, 1_765_290_071UL)]
        [InlineData(30_000_000UL, 1_765_290_070UL, 0x5167e2a6u, 1_765_290_071UL)]
        [InlineData(30_000_000UL, 1_765_290_071UL, 0xcba2a1c0u, 1_767_747_671UL)]
        [InlineData(30_000_000UL, 1_767_747_670UL, 0xcba2a1c0u, 1_767_747_671UL)]
        [InlineData(30_000_000UL, 1_767_747_671UL, 0x07c9462eu, 0UL)]
        [InlineData(50_000_000UL, 2_000_000_000UL, 0x07c9462eu, 0UL)]
        public void ForkId_MainnetThroughBpo2_MatchesGethKnownAnswer(
            ulong headBlock, ulong headTime, uint expectedHash, ulong gethExpectedNext)
        {
            var id = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, GethMainnetBlocks, GethMainnetTimestamps, headBlock, headTime);

            Assert.Equal(expectedHash, id.Hash);
            Assert.Equal(gethExpectedNext, id.Next);
        }

        [Fact]
        public void Given_TheIndependentGethThresholds_When_TheForkIdIsComputedAtCurrentHead_Then_ItIsGethsPostBpo2KnownAnswer()
        {
            var id = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, GethMainnetBlocks, GethMainnetTimestamps,
                headBlock: 30_000_000UL,
                headTime: 1_767_747_671UL);

            Assert.Equal(0x07c9462eu, id.Hash);
            Assert.Equal(0UL, id.Next);
        }

        [Fact]
        public void Given_TheMainnetScheduleDerivedThresholds_When_TheForkIdIsComputedAtCurrentHead_Then_ItMatchesTheIndependentKnownAnswer()
        {
            var id = Eip2124ForkIdCalculator.NewId(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(),
                MainnetChainSchedule.ForkIdentity.BlockHeights,
                MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock: 30_000_000UL,
                headTime: 1_767_747_671UL);

            Assert.Equal(0x07c9462eu, id.Hash);
            Assert.Equal(0UL, id.Next);
        }

        [Fact]
        public void ForkId_MainnetAtGrayGlacier_WrongSchedule_KeepingParis_ProducesDifferentHashAndNext()
        {
            const long parisBlock = 15_537_394;
            var wrongBlocks = new ulong[] {
                1_150_000, 1_920_000, 2_463_000, 2_675_000, 4_370_000, 7_280_000,
                9_069_000, 9_200_000, 12_244_000, 12_965_000, 13_773_000, 15_050_000, (ulong)parisBlock
            };

            var correct = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, GethMainnetBlocks, GethMainnetTimestamps, 15_050_000UL, 0UL);
            var wrong = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, wrongBlocks, GethMainnetTimestamps, 15_050_000UL, 0UL);

            Assert.Equal(correct.Hash, wrong.Hash);
            Assert.NotEqual(correct.Next, wrong.Next);
            Assert.Equal((ulong)parisBlock, wrong.Next);
            Assert.Equal(0xf0afd0e3u, correct.Hash);
            Assert.Equal(1_681_338_455UL, correct.Next);

            var wrongPastParis = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, wrongBlocks, GethMainnetTimestamps, 20_000_000UL, 1_681_338_454UL);
            var correctPastParis = Eip2124ForkIdCalculator.NewId(
                MainnetGenesis, GethMainnetBlocks, GethMainnetTimestamps, 20_000_000UL, 1_681_338_454UL);
            Assert.NotEqual(correctPastParis.Hash, wrongPastParis.Hash);
            Assert.Equal(0xf0afd0e3u, correctPastParis.Hash);
        }

        [Fact]
        public void ForkId_ForkNext_IsNextUpcomingFork_ZeroWhenSynced()
        {
            var id = Eip2124ForkIdCalculator.NewId(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(),
                MainnetChainSchedule.ForkIdentity.BlockHeights,
                MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock: 30_000_000UL,
                headTime: 1_767_747_671UL);

            Assert.Equal(0UL, id.Next);
        }

        [Fact]
        public void ForkId_ForkNext_BeforeAFork_ReportsThatForkNotZero()
        {
            var id = Eip2124ForkIdCalculator.NewId(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(),
                MainnetChainSchedule.ForkIdentity.BlockHeights,
                MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock: 30_000_000UL,
                headTime: 1_764_798_551UL);

            Assert.NotEqual(0UL, id.Next);
            Assert.Equal(1_765_290_071UL, id.Next);
        }

        [Fact]
        public void ForkIdValidation_AcceptsSameForkPeer()
        {
            var genesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var blocks = MainnetChainSchedule.ForkIdentity.BlockHeights;
            var timestamps = MainnetChainSchedule.ForkIdentity.Timestamps;
            var headBlock = 30_000_000UL;
            var headTime = 1_767_747_671UL;

            var ourId = Eip2124ForkIdCalculator.NewId(genesis, blocks, timestamps, headBlock, headTime);

            var result = Eip2124ForkIdCalculator.ValidateForkId(
                ourId.Hash, ourId.Next, genesis, blocks, timestamps, headBlock, headTime);

            Assert.Equal(Eip2124ValidationResult.Accepted, result);
        }

        [Fact]
        public void ForkIdValidation_RejectsWrongForkPeer()
        {
            var genesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var blocks = MainnetChainSchedule.ForkIdentity.BlockHeights;
            var timestamps = MainnetChainSchedule.ForkIdentity.Timestamps;
            var headBlock = 30_000_000UL;
            var headTime = 1_767_747_671UL;

            var result = Eip2124ForkIdCalculator.ValidateForkId(
                0xdeadbeefu, 0UL, genesis, blocks, timestamps, headBlock, headTime);

            Assert.Equal(Eip2124ValidationResult.LocalIncompatibleOrStale, result);
        }

        [Fact]
        public void ForkIdValidation_AcceptsSyncingPeerReportingCorrectNextFork()
        {
            var genesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var blocks = MainnetChainSchedule.ForkIdentity.BlockHeights;
            var timestamps = MainnetChainSchedule.ForkIdentity.Timestamps;

            var peerId = Eip2124ForkIdCalculator.NewId(genesis, blocks, timestamps, 0UL, 0UL);

            var result = Eip2124ForkIdCalculator.ValidateForkId(
                peerId.Hash, peerId.Next, genesis, blocks, timestamps,
                headBlock: 30_000_000UL, headTime: 1_767_747_671UL);

            Assert.Equal(Eip2124ValidationResult.Accepted, result);
        }

        [Fact]
        public void ForkIdValidation_RejectsSyncingPeerReportingWrongNextFork()
        {
            var genesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var blocks = MainnetChainSchedule.ForkIdentity.BlockHeights;
            var timestamps = MainnetChainSchedule.ForkIdentity.Timestamps;

            var peerId = Eip2124ForkIdCalculator.NewId(genesis, blocks, timestamps, 0UL, 0UL);

            var result = Eip2124ForkIdCalculator.ValidateForkId(
                peerId.Hash, 999_999_999UL, genesis, blocks, timestamps,
                30_000_000UL, 1_767_747_671UL);

            Assert.Equal(Eip2124ValidationResult.RemoteStale, result);
        }
    }
}
