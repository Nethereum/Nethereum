using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class HeaderFollowServiceTests
    {
        private static byte[] Hash(ulong n) => Enumerable.Repeat((byte)(n & 0xFF), 32).ToArray();

        private sealed class ScriptedCanonical : ICanonicalStateRootSource
        {
            private readonly Queue<CanonicalTip> _tips;
            private CanonicalTip _last;
            public ScriptedCanonical(params CanonicalTip[] tips) { _tips = new Queue<CanonicalTip>(tips); }
            public string Name => "Scripted";
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null, null));
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                if (_tips.Count > 0) _last = _tips.Dequeue();
                return Task.FromResult(_last);
            }
        }

        private static CanonicalTip Tip(ulong n) =>
            new CanonicalTip { BlockNumber = n, BlockHash = Hash(n), StateRoot = Hash(n) };

        private sealed class ScriptedWalker
        {
            private readonly Queue<WalkerOutcome> _outcomes;
            private WalkerOutcome _last;
            public List<(ulong From, ulong To, ulong NoShortCircuitAbove)> Calls { get; } = new();
            public ScriptedWalker(params WalkerOutcome[] outcomes)
            {
                _outcomes = new Queue<WalkerOutcome>(outcomes);
                _last = outcomes[outcomes.Length - 1];
            }
            public Task<WalkerOutcome> Walk(ulong from, byte[] fromHash, ulong to, IChainStoreBundle bundle, CancellationToken ct, ulong noShortCircuitAboveBlock = ulong.MaxValue)
            {
                Calls.Add((from, to, noShortCircuitAboveBlock));
                if (_outcomes.Count > 0) _last = _outcomes.Dequeue();
                return Task.FromResult(_last);
            }
        }

        private static HeaderFollowService Service(
            ICanonicalStateRootSource canonical, ScriptedWalker walker,
            HeaderFollowOptions options = null, AncestorResolverDelegate resolver = null)
            => new HeaderFollowService(canonical, walker.Walk, options, null, resolver);

        [Fact]
        public async Task AdvanceOnce_TipAdvancedFromEmpty_WalksBoundedGap_RecordsTrueBottom()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.StructuralGenesis, HeadersWritten: 100, DivergenceBlock: null,
                SkeletonBottomBlock: 0));
            var svc = Service(new ScriptedCanonical(Tip(100)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, lastSeenTipBlock: 0, lastSeenTipHash: null, default);

            Assert.Equal(HeaderFollowStatus.Advanced, cycle.Status);
            Assert.Equal(100UL, cycle.TipBlock);
            Assert.Single(walker.Calls);
            Assert.Equal(100UL, walker.Calls[0].From);
            Assert.Equal(0UL, walker.Calls[0].To);
            Assert.Equal(100UL, HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState()));
        }

        [Fact]
        public async Task AdvanceOnce_HugeGap_BoundsTheWalk_OpensDisjointSubchain()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var options = new HeaderFollowOptions { MaxTipWalkBlocks = 4096 };
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.ReachedTarget, HeadersWritten: 4096, DivergenceBlock: null,
                SkeletonBottomBlock: 10_000 - 4096));
            var svc = Service(new ScriptedCanonical(Tip(10_000)), walker, options);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.Advanced, cycle.Status);
            Assert.Equal(10_000UL - 4096, walker.Calls[0].To);
            var state = bundle.Metadata.GetHeaderSyncState();
            Assert.Equal(10_000UL, HeaderSubchains.TrustedTip(state));
            Assert.Single(state.Subchains);
            Assert.Equal(10_000UL - 4096, state.Subchains[0].Tail);
        }

        [Fact]
        public async Task AdvanceOnce_SecondTip_WalksOnlyTheGap_MergesToOneContiguousChain()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.RecordDescent(
                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), 100), 100, 0));

            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.MetExistingStore, HeadersWritten: 50, DivergenceBlock: null,
                SkeletonBottomBlock: 101, MetExistingStore: true));
            var svc = Service(new ScriptedCanonical(Tip(150)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.Advanced, cycle.Status);
            Assert.Equal(150UL, walker.Calls[0].From);
            Assert.Equal(100UL, walker.Calls[0].To);
            var state = bundle.Metadata.GetHeaderSyncState();
            Assert.Equal(150UL, HeaderSubchains.TrustedTip(state));
            Assert.Single(state.Subchains);
            Assert.Equal(0UL, state.Subchains[0].Tail);
        }

        [Fact]
        public async Task AdvanceOnce_PeerPoolEmptyMidWalk_RecordsThePartialRange_NoFabrication()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.PeerPoolEmpty, HeadersWritten: 10, DivergenceBlock: null,
                SkeletonBottomBlock: 91));
            var svc = Service(new ScriptedCanonical(Tip(100)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.PeerPoolEmpty, cycle.Status);
            var state = bundle.Metadata.GetHeaderSyncState();
            Assert.Equal(100UL, HeaderSubchains.TrustedTip(state));
            Assert.Equal(91UL, state.Subchains[0].Tail);
        }

        [Fact]
        public async Task AdvanceOnce_TipNotPastFrontier_ReturnsNotAdvanced_NoWalk()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), 200));
            var walker = new ScriptedWalker(new WalkerOutcome(WalkerExitReason.MetExistingStore, 0, null));
            var svc = Service(new ScriptedCanonical(Tip(200)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.NotAdvanced, cycle.Status);
            Assert.Empty(walker.Calls);
        }

        [Fact]
        public async Task AdvanceOnce_SameTipAsLastSeen_ReturnsNotAdvanced_NoWalk()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(new WalkerOutcome(WalkerExitReason.MetExistingStore, 0, null));
            var svc = Service(new ScriptedCanonical(Tip(300)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, lastSeenTipBlock: 300, lastSeenTipHash: Hash(300), default);

            Assert.Equal(HeaderFollowStatus.NotAdvanced, cycle.Status);
            Assert.Empty(walker.Calls);
        }

        [Fact]
        public async Task AdvanceOnce_Divergence_NoResolver_Surfaced_NoSkeletonRecorded()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.LastKnownGoodDivergence, 0, DivergenceBlock: 77));
            var svc = Service(new ScriptedCanonical(Tip(100)), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.Divergence, cycle.Status);
            Assert.Equal(77UL, cycle.DivergenceBlock);
            Assert.Equal(0UL, HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState()));
        }

        [Fact]
        public async Task AdvanceOnce_Divergence_WithResolver_ForcesRelayAndRecords()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(
                new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, 0, DivergenceBlock: 95),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 55, DivergenceBlock: null,
                    SkeletonBottomBlock: 45, MetExistingStore: true));
            (ulong diverged, ulong floor)? resolverArgs = null;
            AncestorResolverDelegate resolver = (diverged, floor, ct) =>
            {
                resolverArgs = (diverged, floor);
                return Task.FromResult(45UL);
            };
            var svc = Service(new ScriptedCanonical(Tip(100)), walker, resolver: resolver);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.Advanced, cycle.Status);
            Assert.NotNull(resolverArgs);
            Assert.Equal(95UL, resolverArgs.Value.diverged);
            Assert.Equal(2, walker.Calls.Count);
            Assert.Equal(45UL, walker.Calls[1].To);
            Assert.Equal(45UL, walker.Calls[1].NoShortCircuitAbove);
            Assert.Equal(100UL, HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState()));
        }

        [Fact]
        public async Task AdvanceOnce_RelayDivergesAgain_ReResolvesDeeper_Bounded()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(
                new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, 0, DivergenceBlock: 95),
                new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, 0, DivergenceBlock: 60),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 80, DivergenceBlock: null,
                    SkeletonBottomBlock: 20, MetExistingStore: true));
            var resolved = new List<ulong>();
            AncestorResolverDelegate resolver = (diverged, floor, ct) =>
            {
                resolved.Add(diverged);
                return Task.FromResult(diverged - 10);
            };
            var svc = Service(new ScriptedCanonical(Tip(100)), walker, resolver: resolver);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.Advanced, cycle.Status);
            Assert.Equal(new ulong[] { 95, 60 }, resolved);
            Assert.Equal(3, walker.Calls.Count);
            Assert.Equal(50UL, walker.Calls[2].NoShortCircuitAbove);
        }

        [Fact]
        public async Task AdvanceOnce_NullTip_ReturnsSourceUnavailable()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var walker = new ScriptedWalker(new WalkerOutcome(WalkerExitReason.MetExistingStore, 0, null));
            var svc = Service(new ScriptedCanonical((CanonicalTip)null), walker);

            var cycle = await svc.AdvanceOnceAsync(bundle, 0, null, default);

            Assert.Equal(HeaderFollowStatus.SourceUnavailable, cycle.Status);
            Assert.Empty(walker.Calls);
        }


        private static async Task SeedHeaderAsync(IChainStoreBundle bundle, ulong n)
        {
            await bundle.Blocks.SaveAsync(
                new Nethereum.Model.BlockHeader { BlockNumber = n, StateRoot = Hash(n) }, Hash(n));
        }

        [Fact]
        public async Task DescendOnce_OpenGap_WalksOneChunkFromTail_LowersTail()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.RecordDescent(
                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), 1000), 1000, 900));
            await SeedHeaderAsync(bundle, 900);

            var options = new HeaderFollowOptions { DescentChunkBlocks = 200 };
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.ReachedTarget, HeadersWritten: 200, DivergenceBlock: null,
                SkeletonBottomBlock: 700));
            var svc = Service(new ScriptedCanonical(Tip(1000)), walker, options);

            var progressed = await svc.DescendOnceAsync(bundle, default);

            Assert.True(progressed);
            Assert.Equal(900UL, walker.Calls[0].From);
            Assert.Equal(700UL, walker.Calls[0].To);
            var state = bundle.Metadata.GetHeaderSyncState();
            Assert.Equal(700UL, state.Subchains[0].Tail);
        }

        [Fact]
        public async Task DescendOnce_CursorBelowTail_ResumesFromCursor()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.RecordDescent(
                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), 1000), 1000, 900));
            await SeedHeaderAsync(bundle, 900);
            await SeedHeaderAsync(bundle, 850);
            bundle.Metadata.SetLastFetchedHeader(850);

            var options = new HeaderFollowOptions { DescentChunkBlocks = 200 };
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.ReachedTarget, HeadersWritten: 200, DivergenceBlock: null,
                SkeletonBottomBlock: 650));
            var svc = Service(new ScriptedCanonical(Tip(1000)), walker, options);

            var progressed = await svc.DescendOnceAsync(bundle, default);

            Assert.True(progressed);
            Assert.Equal(850UL, walker.Calls[0].From);
            Assert.Equal(650UL, walker.Calls[0].To);
        }

        [Fact]
        public async Task DescendOnce_GapBetweenSubchains_LinksAndMerges()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var hss = HeaderSubchains.RecordDescent(
                HeaderSubchains.OpenTip(HeaderSyncState.Empty, 1000), 1000, 0);
            hss = HeaderSubchains.RecordDescent(HeaderSubchains.OpenTip(hss, 10_000), 10_000, 5904);
            bundle.Metadata.SaveHeaderSyncState(hss);
            await SeedHeaderAsync(bundle, 5904);

            var options = new HeaderFollowOptions { DescentChunkBlocks = 10_000 };
            var walker = new ScriptedWalker(new WalkerOutcome(
                WalkerExitReason.MetExistingStore, HeadersWritten: 4903, DivergenceBlock: null,
                SkeletonBottomBlock: 1001, MetExistingStore: true));
            var svc = Service(new ScriptedCanonical(Tip(10_000)), walker, options);

            var progressed = await svc.DescendOnceAsync(bundle, default);

            Assert.True(progressed);
            Assert.Equal(5904UL, walker.Calls[0].From);
            Assert.Equal(1001UL, walker.Calls[0].To);
            var state = bundle.Metadata.GetHeaderSyncState();
            Assert.Single(state.Subchains);
            Assert.Equal(0UL, state.Subchains[0].Tail);
            Assert.Equal(10_000UL, state.Subchains[0].Head);
        }

        [Fact]
        public async Task DescendOnce_NoGap_ReturnsFalse_NoWalk()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.RecordDescent(
                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), 1000), 1000, 0));

            var walker = new ScriptedWalker(new WalkerOutcome(WalkerExitReason.ReachedTarget, 0, null));
            var svc = Service(new ScriptedCanonical(Tip(1000)), walker);

            var progressed = await svc.DescendOnceAsync(bundle, default);

            Assert.False(progressed);
            Assert.Empty(walker.Calls);
        }
    }
}
