using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class HistoryBackfillModeTests
    {
        [Fact]
        public void Given_NotAfterStateSync_When_Planning_Then_NotDeferred()
        {
            Assert.Equal(
                DeferredHistoryBackfillDecision.NotDeferred,
                DeferredHistoryBackfillPlanner.Decide(
                    runAfterStateSync: false, peeringAvailable: true, headerSkeletonAvailable: true, pivot: 100, cursor: 0));
        }

        [Fact]
        public void Given_AfterStateSyncButNoPeering_When_Planning_Then_PeeringUnavailable()
        {
            Assert.Equal(
                DeferredHistoryBackfillDecision.PeeringUnavailable,
                DeferredHistoryBackfillPlanner.Decide(
                    runAfterStateSync: true, peeringAvailable: false, headerSkeletonAvailable: true, pivot: 100, cursor: 0));
        }

        [Fact]
        public void Given_AfterStateSyncButNoHeaderSkeleton_When_Planning_Then_HeaderSkeletonUnavailable()
        {
            Assert.Equal(
                DeferredHistoryBackfillDecision.HeaderSkeletonUnavailable,
                DeferredHistoryBackfillPlanner.Decide(
                    runAfterStateSync: true, peeringAvailable: true, headerSkeletonAvailable: false, pivot: 100, cursor: 0));
        }

        [Fact]
        public void Given_HeaderSkeletonAvailableAndWorkToDo_When_Planning_Then_Run()
        {
            Assert.Equal(
                DeferredHistoryBackfillDecision.Run,
                DeferredHistoryBackfillPlanner.Decide(
                    runAfterStateSync: true, peeringAvailable: true, headerSkeletonAvailable: true, pivot: 100, cursor: 0));
        }

        [Theory]
        [InlineData(0UL, 0UL)]
        [InlineData(100UL, 100UL)]
        [InlineData(100UL, 150UL)]
        public void Given_AfterStateSyncButNothingBehindPivot_When_Planning_Then_NothingToFill(ulong pivot, ulong cursor)
        {
            Assert.Equal(
                DeferredHistoryBackfillDecision.NothingToFill,
                DeferredHistoryBackfillPlanner.Decide(
                    runAfterStateSync: true, peeringAvailable: true, headerSkeletonAvailable: true, pivot: pivot, cursor: cursor));
        }

        [Fact]
        public void Given_DefaultConfig_When_Read_Then_HistoryBackfillIsDuringStateSync()
        {
            var config = new MainnetChainServerConfig();
            Assert.Equal(HistoryBackfillMode.DuringStateSync, config.HistoryBackfill);
            Assert.True(config.RunHistoryBackfillDuringStateSync);
            Assert.False(config.RunHistoryBackfillAfterStateSync);
        }

        [Fact]
        public void Given_ModeDuringStateSync_When_DecidingBackfill_Then_DuringTrueAfterFalse()
        {
            var config = new MainnetChainServerConfig { HistoryBackfill = HistoryBackfillMode.DuringStateSync };
            Assert.True(config.RunHistoryBackfillDuringStateSync);
            Assert.False(config.RunHistoryBackfillAfterStateSync);
        }

        [Fact]
        public void Given_ModeAfterStateSync_When_DecidingBackfill_Then_AfterTrueDuringFalse()
        {
            var config = new MainnetChainServerConfig { HistoryBackfill = HistoryBackfillMode.AfterStateSync };
            Assert.True(config.RunHistoryBackfillAfterStateSync);
            Assert.False(config.RunHistoryBackfillDuringStateSync);
        }

        [Fact]
        public void Given_ModeNever_When_DecidingBackfill_Then_NeitherDuringNorAfter()
        {
            var config = new MainnetChainServerConfig { HistoryBackfill = HistoryBackfillMode.Never };
            Assert.False(config.RunHistoryBackfillDuringStateSync);
            Assert.False(config.RunHistoryBackfillAfterStateSync);
        }
    }
}
