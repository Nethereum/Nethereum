using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncOrchestratorPivotFloorTests
    {
        [Fact]
        public void FreshSync_NoSavedFloor_AlwaysMeets()
        {
            Assert.True(SnapSyncOrchestrator.CanonicalPivotMeetsResumeFloor(100, 0));
            Assert.True(SnapSyncOrchestrator.CanonicalPivotMeetsResumeFloor(25_446_754, 0));
        }

        [Fact]
        public void TipBelowSavedPivot_DoesNotMeet_SoOrchestratorWaits()
        {
            Assert.False(SnapSyncOrchestrator.CanonicalPivotMeetsResumeFloor(25_386_927, 25_445_471));
        }

        [Fact]
        public void TipAtOrAboveSavedPivot_Meets()
        {
            Assert.True(SnapSyncOrchestrator.CanonicalPivotMeetsResumeFloor(25_445_471, 25_445_471));
            Assert.True(SnapSyncOrchestrator.CanonicalPivotMeetsResumeFloor(25_446_754, 25_445_471));
        }


        [Fact]
        public void PivotIsStale_OnlyPastTheServingWindowMargin()
        {
            Assert.False(SnapSyncOrchestrator.PivotIsStale(1000, 1000));
            Assert.False(SnapSyncOrchestrator.PivotIsStale(1000, 1120));
            Assert.True(SnapSyncOrchestrator.PivotIsStale(1000, 1121));
        }


        [Fact]
        public void ForceFresh_StalledHealUnder120_RotatesToFresherServableRoot()
        {
            Assert.True(SnapSyncOrchestrator.ShouldRefreshTrailedPivot(
                forceFresh: true, currentPivotBlock: 1000, tipBlock: 1060,
                candidatePivotBlock: 1028, lastReturnedPivotBlock: 1000));
        }

        [Fact]
        public void PeriodicPoll_Under120_DoesNotRotate()
        {
            Assert.False(SnapSyncOrchestrator.ShouldRefreshTrailedPivot(
                forceFresh: false, currentPivotBlock: 1000, tipBlock: 1060,
                candidatePivotBlock: 1028, lastReturnedPivotBlock: 1000));
        }

        [Fact]
        public void PeriodicPoll_PastStaleDistance_Rotates()
        {
            Assert.True(SnapSyncOrchestrator.ShouldRefreshTrailedPivot(
                forceFresh: false, currentPivotBlock: 1000, tipBlock: 1200,
                candidatePivotBlock: 1168, lastReturnedPivotBlock: 1000));
        }

        [Fact]
        public void ForceFresh_MonotonicDedup_NeverReturnsSameOrEarlierPivot()
        {
            Assert.False(SnapSyncOrchestrator.ShouldRefreshTrailedPivot(
                forceFresh: true, currentPivotBlock: 1000, tipBlock: 1060,
                candidatePivotBlock: 1028, lastReturnedPivotBlock: 1028));
            Assert.False(SnapSyncOrchestrator.ShouldRefreshTrailedPivot(
                forceFresh: true, currentPivotBlock: 1000, tipBlock: 1060,
                candidatePivotBlock: 1010, lastReturnedPivotBlock: 1028));
        }

        private sealed class FixedTipCanonicalSource : ICanonicalStateRootSource
        {
            public CanonicalTip Tip { get; set; }
            public string Name => "test-fixed-tip";
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(Tip);
        }

        [Fact]
        public async Task Refresher_RollProducedButNotAdopted_RollingPivotStillBehind_NextTickStillOffersFreshPivot()
        {
            const ulong initialPivotBlock = 900;
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 1060, laidTip: 1060);

            var rollingPivot = new SnapBootstrapper.RollingPivot(
                new BlockHeader { BlockNumber = initialPivotBlock, StateRoot = Root(initialPivotBlock) },
                Hash(initialPivotBlock));

            var tipSource = new FixedTipCanonicalSource { Tip = Tip(1050) };
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                pool: null, scheduler: null, canonicalTip: tipSource, logger: NullLogger.Instance,
                rollingPivot: rollingPivot, bundle: bundle);

            var roll1 = await refresher(false, CancellationToken.None);
            Assert.NotNull(roll1);
            Assert.Equal(1018UL, (ulong)roll1.Value.Header.BlockNumber);
            Assert.Equal(initialPivotBlock, (ulong)rollingPivot.Current.Header.BlockNumber);

            tipSource.Tip = Tip(1060);
            var roll2 = await refresher(false, CancellationToken.None);

            Assert.NotNull(roll2);
            Assert.Equal(1028UL, (ulong)roll2.Value.Header.BlockNumber);
            Assert.Equal(initialPivotBlock, (ulong)rollingPivot.Current.Header.BlockNumber);
        }


        private static byte[] Hash(ulong n)
        {
            var h = new byte[32];
            h[0] = 0x10;
            h[30] = (byte)(n >> 8);
            h[31] = (byte)n;
            return h;
        }

        private static byte[] Root(ulong n) => Enumerable.Repeat((byte)(n & 0xFF), 32).ToArray();

        private static async Task SeedLinkedAsync(IChainStoreBundle bundle, ulong from, ulong to, ulong? laidTip = null)
        {
            for (ulong n = from; n <= to; n++)
            {
                await bundle.Blocks.SaveAsync(new BlockHeader
                {
                    BlockNumber = n,
                    StateRoot = Root(n),
                    ParentHash = n > 0 ? Hash(n - 1) : new byte[32],
                }, Hash(n));
            }
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, laidTip ?? to));
        }

        private static CanonicalTip Tip(ulong n) =>
            new CanonicalTip { BlockNumber = n, BlockHash = Hash(n), StateRoot = Root(n) };

        [Fact]
        public async Task TrailedPivot_VerifiedWindow_ReturnsHeaderAtTipMinusTrail()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 1000);

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(1000), CancellationToken.None);

            Assert.NotNull(pivot);
            Assert.Equal(1000UL - SnapSyncOrchestrator.PivotServeTrailDistance, (ulong)pivot.Value.Header.BlockNumber);
            Assert.Equal(Root(1000 - SnapSyncOrchestrator.PivotServeTrailDistance), pivot.Value.Header.StateRoot);
            Assert.Equal(Hash(1000 - SnapSyncOrchestrator.PivotServeTrailDistance), pivot.Value.Hash);
        }

        [Fact]
        public async Task TrailedPivot_LiveTipAheadOfLaidFrontier_RollsToTrustedTipMinusTrail()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 990, laidTip: 990);

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(1000), CancellationToken.None);

            Assert.NotNull(pivot);
            Assert.Equal(990UL - SnapSyncOrchestrator.PivotServeTrailDistance, (ulong)pivot.Value.Header.BlockNumber);
            Assert.Equal(Root(990 - SnapSyncOrchestrator.PivotServeTrailDistance), pivot.Value.Header.StateRoot);
            Assert.Equal(Hash(990 - SnapSyncOrchestrator.PivotServeTrailDistance), pivot.Value.Hash);
        }


        [Fact]
        public async Task TrailedPivot_LaidFrontierAtOrBelowTrailDistance_ReturnsNull()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 20, laidTip: 20);

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(20), CancellationToken.None);

            Assert.Null(pivot);
        }

        [Fact]
        public async Task TrailedPivot_NoHeaderSyncStateLaidAtAll_ReturnsNull()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            for (ulong n = 0; n <= 1000; n++)
            {
                await bundle.Blocks.SaveAsync(new BlockHeader
                {
                    BlockNumber = n,
                    StateRoot = Root(n),
                    ParentHash = n > 0 ? Hash(n - 1) : new byte[32],
                }, Hash(n));
            }

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(1000), CancellationToken.None);

            Assert.Null(pivot);
        }

        [Fact]
        public async Task TrailedPivot_AnchorRowMissingDespiteLaidMetadata_ReturnsNull()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 990, laidTip: 990);
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, 1000));

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(1000), CancellationToken.None);

            Assert.Null(pivot);
        }

        [Fact]
        public async Task TrailedPivot_BrokenLinkInsideWindow_ReturnsNull()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SeedLinkedAsync(bundle, 0, 1000);
            await bundle.Blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = 990,
                StateRoot = Root(990),
                ParentHash = Root(0xDD),
            }, Hash(990));

            var pivot = await SnapSyncOrchestrator.TrailedPivotFromStoreAsync(bundle, Tip(1000), CancellationToken.None);

            Assert.Null(pivot);
        }


        [Fact]
        public void Given_AttemptBelowEscalationThreshold_When_ComputingRetryEscalation_Then_NotEscalatedAndCapIsTheOriginalThirtySeconds()
        {
            var (escalated, cap) = SnapSyncOrchestrator.ComputeRetryEscalation(SnapSyncOrchestrator.SnapAttemptEscalationThreshold - 1);

            Assert.False(escalated);
            Assert.Equal(SnapSyncOrchestrator.SnapAttemptMaxBackoffMs, cap);
        }

        [Fact]
        public void Given_AttemptAtOrAboveEscalationThreshold_When_ComputingRetryEscalation_Then_EscalatedAndCapExceedsTheOldThirtySecondCap()
        {
            var (escalated, cap) = SnapSyncOrchestrator.ComputeRetryEscalation(SnapSyncOrchestrator.SnapAttemptEscalationThreshold);

            Assert.True(escalated);
            Assert.True(cap > SnapSyncOrchestrator.SnapAttemptMaxBackoffMs);
            Assert.Equal(SnapSyncOrchestrator.SnapAttemptMaxBackoffMsEscalated, cap);
        }
    }
}
