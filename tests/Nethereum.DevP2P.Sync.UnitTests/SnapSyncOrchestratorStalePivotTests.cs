using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncOrchestratorStalePivotTests
    {
        private const ulong VerifiedTip = 20_000;
        private const ulong LaggingLaidTip = 3_600;
        private const ulong StartBound = SnapSyncOrchestrator.PivotTrailDistance;
        private const ulong ServeTrail = SnapSyncOrchestrator.PivotServeTrailDistance;
        private const ulong ConfiguredStaleDistance = 40;
        private static readonly ulong StalePivot = LaggingLaidTip - ServeTrail;
        private static readonly ulong NearTipPivot = VerifiedTip - ServeTrail;
        private static readonly ulong LaidTipAtBound = VerifiedTip - StartBound + ServeTrail;
        private static readonly ulong LaidTipAtConfiguredBound = VerifiedTip - ConfiguredStaleDistance + ServeTrail;

        private sealed class FixedTipSource : ICanonicalStateRootSource
        {
            private readonly byte[] _tipHash;
            public FixedTipSource(byte[] tipHash = null) => _tipHash = tipHash ?? Hash(VerifiedTip);
            public string Name => "fixed-tip";
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(new CanonicalTip { BlockNumber = VerifiedTip, BlockHash = _tipHash, StateRoot = Root(VerifiedTip) });
        }

        private sealed class EmptyPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class SilentPeersScheduler : IFetchRequestScheduler
        {
            private readonly ConcurrentQueue<byte[]> _requestedRoots = new();
            public IReadOnlyList<byte[]> RequestedRoots => _requestedRoots.ToList();
            private static async Task<T> NeverAnswers<T>(CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                throw new OperationCanceledException(ct);
            }
            private Task<T> Record<T>(byte[] stateRoot, CancellationToken ct)
            {
                _requestedRoots.Enqueue(stateRoot);
                return NeverAnswers<T>(ct);
            }
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => NeverAnswers<List<BlockHeader>>(ct);
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => NeverAnswers<List<BlockBody>>(ct);
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => NeverAnswers<BodyFetchResult>(ct);
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => NeverAnswers<List<List<Receipt>>>(ct);
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => Record<AccountRangeMessage>(stateRoot, ct);
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => Record<StorageRangesMessage>(stateRoot, ct);
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => NeverAnswers<ByteCodesMessage>(ct);
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => Record<TrieNodesMessage>(stateRoot, ct);
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages = new();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _messages.Enqueue(formatter(state, exception));
            public bool Saw(string fragment) => _messages.Any(m => m.Contains(fragment, StringComparison.Ordinal));
        }

        private sealed class Run
        {
            public InMemoryChainStoreBundle Bundle { get; init; }
            public RecordingLogger Log { get; } = new();
            public SilentPeersScheduler Scheduler { get; } = new();
            public CancellationTokenSource Cts { get; init; }
            public Task<SnapBootstrapper.Result> Task { get; set; }
        }

        private static byte[] Hash(ulong n)
        {
            var h = new byte[32];
            h[0] = 0x10;
            h[29] = (byte)(n >> 16);
            h[30] = (byte)(n >> 8);
            h[31] = (byte)n;
            return h;
        }

        private static byte[] Root(ulong n)
        {
            var r = new byte[32];
            r[0] = 0x20;
            r[29] = (byte)(n >> 16);
            r[30] = (byte)(n >> 8);
            r[31] = (byte)n;
            return r;
        }

        private static async Task LayHeadersAsync(IChainStoreBundle bundle, ulong from, ulong laidTip)
        {
            for (ulong n = from; n <= laidTip; n++)
                await bundle.Blocks.SaveAsync(new BlockHeader
                {
                    BlockNumber = n,
                    StateRoot = Root(n),
                    ParentHash = n > 0 ? Hash(n - 1) : new byte[32],
                }, Hash(n));
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, laidTip));
        }

        private static async Task<Run> StartAsync(ulong laidTip, bool balHealEnabled = false,
            ulong staleDistanceBlocks = SnapSyncOrchestrator.PivotStaleDistanceBlocks, byte[] tipHash = null)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await LayHeadersAsync(bundle, 0, laidTip);
            var run = new Run { Bundle = bundle, Cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)) };
            var tip = new FixedTipSource(tipHash);
            run.Task = SnapSyncOrchestrator.RunAsync(
                bundle, new EmptyPool(), run.Scheduler, tip, activations: null, run.Log,
                new SnapSyncOrchestratorOptions
                {
                    HeaderFollow = new HeaderFollowService(tip, (from, hash, to, b, ct, above) => Task.FromResult<WalkerOutcome>(null)),
                    RunHistoryBackfill = false,
                    BalHealEnabled = balHealEnabled,
                    PivotStaleDistanceBlocks = staleDistanceBlocks,
                },
                run.Cts.Token);
            return run;
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (condition()) return true;
                await Task.Delay(25);
            }
            return condition();
        }

        private static async Task StopAsync(Run run)
        {
            run.Cts.Cancel();
            try { await run.Task; }
            catch (OperationCanceledException) { }
            run.Cts.Dispose();
            run.Bundle.Dispose();
        }

        private static string AnchoredAt(ulong block) => $"pivot anchored on verified local chain block={block} ";

        private static async Task AssertAnchorsWithoutWaitingAsync(ulong laidTip, ulong expectedPivot,
            bool balHealEnabled = false, ulong staleDistanceBlocks = SnapSyncOrchestrator.PivotStaleDistanceBlocks)
        {
            var run = await StartAsync(laidTip, balHealEnabled, staleDistanceBlocks);
            try
            {
                Assert.True(await WaitUntilAsync(() => run.Log.Saw(AnchoredAt(expectedPivot)), TimeSpan.FromSeconds(10)));
                Assert.False(run.Log.Saw("snap.bootstrap.pivot_wait"));
            }
            finally { await StopAsync(run); }
        }

        private static async Task AssertWaitsWithoutAnchoringAsync(ulong laidTip,
            bool balHealEnabled = false, ulong staleDistanceBlocks = SnapSyncOrchestrator.PivotStaleDistanceBlocks)
        {
            var run = await StartAsync(laidTip, balHealEnabled, staleDistanceBlocks);
            try
            {
                Assert.True(await WaitUntilAsync(() => run.Log.Saw("snap.bootstrap.pivot_wait"), TimeSpan.FromSeconds(10)));
                Assert.True(run.Log.Saw($"trailed_pivot={laidTip - ServeTrail} ") && run.Log.Saw($"laid_tip={laidTip})"));
                Assert.False(run.Log.Saw("pivot anchored on verified local chain"));
                Assert.DoesNotContain(run.Scheduler.RequestedRoots, r => ByteUtil.AreEqual(r, Root(laidTip - ServeTrail)));
            }
            finally { await StopAsync(run); }
        }

        [Fact]
        public Task Given_TheVerifiedTipFarAheadOfLaidHeaders_When_Snap1SelectsAPivot_Then_ItWaitsForTheHeadersJobInsteadOfAnchoringOnTheStaleLocalPivot()
            => AssertWaitsWithoutAnchoringAsync(LaggingLaidTip);

        [Fact]
        public Task Given_TheVerifiedTipFarAheadOfLaidHeaders_When_Snap2SelectsAPivot_Then_ItWaitsForTheHeadersJobLikeSnap1()
            => AssertWaitsWithoutAnchoringAsync(LaggingLaidTip, balHealEnabled: true);

        [Fact]
        public Task Given_LaidHeadersPuttingThePivotExactlyFsMinFullBlocksBelowTheTip_When_Snap1SelectsAPivot_Then_ItAnchorsThere()
            => AssertAnchorsWithoutWaitingAsync(LaidTipAtBound, VerifiedTip - StartBound);

        [Fact]
        public Task Given_LaidHeadersPuttingThePivotOneBlockPastFsMinFullBlocksBelowTheTip_When_Snap1SelectsAPivot_Then_ItWaits()
            => AssertWaitsWithoutAnchoringAsync(LaidTipAtBound - 1);

        [Fact]
        public Task Given_LaidHeadersPuttingThePivotOneBlockPastFsMinFullBlocksBelowTheTip_When_Snap2SelectsAPivot_Then_ItWaits()
            => AssertWaitsWithoutAnchoringAsync(LaidTipAtBound - 1, balHealEnabled: true);

        [Fact]
        public Task Given_AConfiguredStaleDistanceInsideFsMinFullBlocksAndThePivotExactlyThatFarBelowTheTip_When_Snap1SelectsAPivot_Then_ItAnchorsThere()
            => AssertAnchorsWithoutWaitingAsync(LaidTipAtConfiguredBound, VerifiedTip - ConfiguredStaleDistance,
                staleDistanceBlocks: ConfiguredStaleDistance);

        [Fact]
        public Task Given_AConfiguredStaleDistanceInsideFsMinFullBlocksAndThePivotOneBlockPastIt_When_Snap1SelectsAPivot_Then_ItWaitsBecauseTheRefresherWouldAlreadyCallItStale()
            => AssertWaitsWithoutAnchoringAsync(LaidTipAtConfiguredBound - 1, staleDistanceBlocks: ConfiguredStaleDistance);

        [Fact]
        public Task Given_LaidHeadersAtTheVerifiedTip_When_Snap1SelectsAPivot_Then_ItAnchorsImmediatelyWithoutWaiting()
            => AssertAnchorsWithoutWaitingAsync(VerifiedTip, NearTipPivot);

        [Fact]
        public Task Given_LaidHeadersAtTheVerifiedTip_When_Snap2SelectsAPivot_Then_ItAnchorsImmediatelyWithoutWaiting()
            => AssertAnchorsWithoutWaitingAsync(VerifiedTip, NearTipPivot, balHealEnabled: true);

        [Fact]
        public Task Given_LaidHeadersAtTheVerifiedTipAndAStaleDistanceShorterThanTheServeTrail_When_Snap1SelectsAPivot_Then_ItAnchorsImmediatelyWithoutWaiting()
            => AssertAnchorsWithoutWaitingAsync(VerifiedTip, NearTipPivot, staleDistanceBlocks: ServeTrail / 2);

        [Fact]
        public async Task Given_Snap1WaitingOnLaggingHeaders_When_TheHeadersJobLaysHeadersToTheTip_Then_ItAnchorsTheServeTrailBelowTheVerifiedTip()
        {
            var run = await StartAsync(LaggingLaidTip);
            try
            {
                Assert.True(await WaitUntilAsync(() => run.Log.Saw("snap.bootstrap.pivot_wait"), TimeSpan.FromSeconds(10)));

                await LayHeadersAsync(run.Bundle, LaggingLaidTip + 1, VerifiedTip);

                Assert.True(await WaitUntilAsync(() => run.Log.Saw(AnchoredAt(NearTipPivot)), TimeSpan.FromSeconds(15)));
                Assert.False(run.Log.Saw(AnchoredAt(StalePivot)));
                Assert.DoesNotContain(run.Scheduler.RequestedRoots, r => ByteUtil.AreEqual(r, Root(StalePivot)));
            }
            finally { await StopAsync(run); }
        }

        [Fact]
        public async Task Given_Snap1WaitingOnLaggingHeaders_When_TheRunIsCancelled_Then_ItReturnsPromptlyAsCancelledWhileFetchingThePivot()
        {
            var run = await StartAsync(LaggingLaidTip);
            try
            {
                Assert.True(await WaitUntilAsync(() => run.Log.Saw("snap.bootstrap.pivot_wait"), TimeSpan.FromSeconds(10)));

                run.Cts.Cancel();
                var completed = await Task.WhenAny(run.Task, Task.Delay(TimeSpan.FromSeconds(5)));

                Assert.Same(run.Task, completed);
                var result = await run.Task;
                Assert.False(result.Ran);
                Assert.Equal("cancelled while fetching pivot", result.SkipReason);
                Assert.False(run.Log.Saw("pivot anchored on verified local chain"));
            }
            finally { await StopAsync(run); }
        }

        private static async Task<((BlockHeader Header, byte[] Hash)? Refreshed, RecordingLogger Log)> RefreshFromLaidTipAsync(ulong laidTip)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await LayHeadersAsync(bundle, 0, laidTip);
            ulong current = LaggingLaidTip - ServeTrail;
            var rolling = new SnapBootstrapper.RollingPivot(
                new BlockHeader { BlockNumber = current, StateRoot = Root(current) }, Hash(current));
            var log = new RecordingLogger();
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new EmptyPool(), new SilentPeersScheduler(), new FixedTipSource(), log, rolling, bundle);
            return (await refresher(false, CancellationToken.None), log);
        }

        private static async Task AssertRefresherWaitsForLaggingHeadersAsync(ulong laidTip)
        {
            var (refreshed, log) = await RefreshFromLaidTipAsync(laidTip);

            Assert.Null(refreshed);
            Assert.True(log.Saw("reason=laid_headers_lag") && log.Saw($"trailed_pivot={laidTip - ServeTrail} "));
        }

        private static async Task AssertRefresherMovesToAsync(ulong laidTip, ulong expectedPivot)
        {
            var (refreshed, _) = await RefreshFromLaidTipAsync(laidTip);

            Assert.NotNull(refreshed);
            Assert.Equal(expectedPivot, (ulong)refreshed.Value.Header.BlockNumber);
        }

        [Fact]
        public Task Given_AStalePivotAndLaidHeadersStillLaggingTheTip_When_ThePivotRefresherRuns_Then_ItWaitsInsteadOfMovingToAnotherStalePivot()
            => AssertRefresherWaitsForLaggingHeadersAsync(LaggingLaidTip + 1_000);

        [Fact]
        public Task Given_AStalePivotAndLaidHeadersPuttingTheCandidateExactlyFsMinFullBlocksBelowTheTip_When_ThePivotRefresherRuns_Then_ItMovesThere()
            => AssertRefresherMovesToAsync(LaidTipAtBound, VerifiedTip - StartBound);

        [Fact]
        public Task Given_AStalePivotAndLaidHeadersPuttingTheCandidateOneBlockPastFsMinFullBlocksBelowTheTip_When_ThePivotRefresherRuns_Then_ItWaits()
            => AssertRefresherWaitsForLaggingHeadersAsync(LaidTipAtBound - 1);

        [Fact]
        public Task Given_AStalePivotAndLaidHeadersAtTheTip_When_ThePivotRefresherRuns_Then_ItMovesToTheServeTrailBelowTheTip()
            => AssertRefresherMovesToAsync(VerifiedTip, NearTipPivot);

        [Fact]
        public async Task Given_LaidHeadersAtTheTipNumberButAStoredHashThatIsNotTheVerifiedTipHash_When_Snap1SelectsAPivot_Then_ItWaitsInsteadOfAnchoringOnTheUnverifiedChain()
        {
            var otherTipHash = Hash(VerifiedTip);
            otherTipHash[0] = 0x99;
            var run = await StartAsync(VerifiedTip, tipHash: otherTipHash);
            try
            {
                Assert.True(await WaitUntilAsync(() => run.Log.Saw("snap.bootstrap.pivot_wait headers not yet laid/verified"), TimeSpan.FromSeconds(10)));
                Assert.False(run.Log.Saw("pivot anchored on verified local chain"));
            }
            finally { await StopAsync(run); }
        }
    }
}
