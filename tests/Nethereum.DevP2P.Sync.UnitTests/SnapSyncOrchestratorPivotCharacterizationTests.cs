using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncOrchestratorPivotCharacterizationTests
    {
        private const ulong Trail = SnapSyncOrchestrator.PivotTrailDistance;

        private static CanonicalTip Tip(ulong block, bool withRoot = true) => new()
        {
            BlockNumber = block,
            BlockHash = withRoot ? CharBytes.Hash32(0x10) : Array.Empty<byte>(),
            StateRoot = withRoot ? CharBytes.Hash32(0x20) : Array.Empty<byte>(),
        };

        private sealed class Started : IDisposable
        {
            public CharLog Log { get; } = new();
            public CancellationTokenSource Cts { get; } = new();
            public InMemoryChainStoreBundle Bundle { get; }
            public Task<SnapBootstrapper.Result> Task { get; }

            public Started(ICanonicalStateRootSource tipSource, CharScheduler scheduler, InMemoryChainStoreBundle bundle = null)
            {
                Bundle = bundle ?? InMemoryChainStoreBundle.Open();
                Task = SnapSyncOrchestrator.RunAsync(
                    Bundle, new CharPool(), scheduler, tipSource, activations: null, Log,
                    new SnapSyncOrchestratorOptions
                    {
                        UseBackwardSkeleton = false,
                        RunHistoryBackfill = false,
                        AccountConcurrency = 1,
                    },
                    Cts.Token);
            }

            public async Task<SnapBootstrapper.Result> StopAsync()
            {
                Cts.Cancel();
                try { return await Task; }
                catch (OperationCanceledException) { return null; }
            }

            public void Dispose() => Cts.Dispose();
        }

        [Fact]
        public async Task Given_ABundleWithCommittedState_When_RunIsCalled_Then_ItSkipsWithoutTouchingThePeers()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(5, CharBytes.Hash32(0x01));
            var log = new CharLog();

            var result = await SnapSyncOrchestrator.RunAsync(
                bundle, new CharPool(), new CharScheduler(), new CharTipSource(Tip(1_000)), activations: null, log);

            Assert.False(result.Ran);
            Assert.Equal("existing state at block 5", result.SkipReason);
            Assert.Equal(
                "snap.bootstrap.skip reason=committed_state block=5",
                Assert.Single(log.Entries).Message);
        }

        [Fact]
        public async Task Given_NoPeerPoolOrScheduler_When_RunIsCalled_Then_ItSkipsWithTheRegistrationReason()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var log = new CharLog();

            var result = await SnapSyncOrchestrator.RunAsync(
                bundle, pool: null, scheduler: null, new CharTipSource(Tip(1_000)), activations: null, log);

            Assert.False(result.Ran);
            Assert.Equal("no peer pool / scheduler registered", result.SkipReason);
            Assert.Equal(
                "snap.bootstrap.skip reason=no_pool_or_scheduler",
                Assert.Single(log.Entries).Message);
        }

        [Fact]
        public async Task Given_ACanonicalTipSourceThatThrows_When_TheFirstPollFails_Then_ItIsLoggedAndRetriedAfterTheInitialPivotBackoff()
        {
            var calls = 0;
            var source = new CharTipSource(_ =>
                ++calls == 1 ? throw new InvalidOperationException("rpc down") : Task.FromResult(Tip(1_000)));
            using var run = new Started(source, new CharScheduler());

            Assert.True(await run.Log.WaitForAsync("pivot anchored on canonical tip", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            var warn = Assert.Single(run.Log.With("canonical tip source char-tip threw"));
            Assert.Equal(LogLevel.Warning, warn.Level);
            Assert.Equal(
                $"Snap-bootstrap: canonical tip source char-tip threw; retrying in {SnapSyncOrchestrator.PivotFetchInitialBackoffMs}ms",
                warn.Message);
            Assert.IsType<InvalidOperationException>(warn.Exception);
            Assert.Equal(
                $"Snap-bootstrap: pivot anchored on canonical tip block=1000 stateRoot=0x{CharBytes.Hash32(0x20).ToHex()}",
                Assert.Single(run.Log.With("pivot anchored on canonical tip")).Message);
        }

        [Fact]
        public async Task Given_ACanonicalTipAtOrBelowTheTrailDistance_When_ThePivotIsFetched_Then_ItWaitsForAHigherTipBeforeAnchoring()
        {
            var calls = 0;
            var source = new CharTipSource(_ => Task.FromResult(++calls == 1 ? Tip(Trail) : Tip(1_000)));
            using var run = new Started(source, new CharScheduler());

            Assert.True(await run.Log.WaitForAsync("pivot anchored on canonical tip", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            Assert.True(calls >= 2);
            Assert.Contains("block=1000", Assert.Single(run.Log.With("pivot anchored on canonical tip")).Message);
        }

        [Fact]
        public async Task Given_ASavedPivotAboveTheTrailedTip_When_ThePivotIsFetched_Then_ItWaitsWithoutRegressingBelowTheSavedPivot()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 990,
                PivotBlockHash = CharBytes.Hash32(0x33),
                HealTargetRoot = CharBytes.Hash32(0x34),
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            var calls = 0;
            var source = new CharTipSource(_ => Task.FromResult(++calls == 1 ? Tip(1_000) : Tip(1_200)));
            using var run = new Started(source, new CharScheduler(), bundle);

            Assert.True(await run.Log.WaitForAsync("pivot anchored on canonical tip", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            Assert.Equal(
                "snap.bootstrap.resume_floor saved_pivot=990 — will not adopt a pivot below this",
                Assert.Single(run.Log.With("snap.bootstrap.resume_floor")).Message);
            Assert.Equal(
                "snap.bootstrap.pivot_wait trailed=968 < saved_pivot=990 — waiting for canonical tip to reach the saved pivot (no regression)",
                Assert.Single(run.Log.With("snap.bootstrap.pivot_wait")).Message);
            Assert.Contains("block=1200", Assert.Single(run.Log.With("pivot anchored on canonical tip")).Message);
        }

        [Fact]
        public async Task Given_ATipWithoutAStateRoot_When_ThePivotIsFetched_Then_TheHeaderIsFetchedFromThePeersAtTipMinusTheTrailDistance()
        {
            var header = new BlockHeader { BlockNumber = 1_000 - (long)Trail, StateRoot = CharBytes.Hash32(0x44), ParentHash = CharBytes.Hash32(0x45) };
            var requested = new List<ulong>();
            var scheduler = new CharScheduler
            {
                Headers = (start, limit, ct) =>
                {
                    requested.Add(start);
                    return Task.FromResult(new List<BlockHeader> { header });
                },
            };
            using var run = new Started(new CharTipSource(Tip(1_000, withRoot: false)), scheduler);

            Assert.True(await run.Log.WaitForAsync("pivot header received", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            Assert.Equal(new[] { 1_000UL - Trail }, requested.Take(1));
            var expectedHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
            Assert.Equal(
                $"Snap-bootstrap: pivot header received block=936 hash=0x{expectedHash.ToHex()} stateRoot=0x{header.StateRoot.ToHex()}; running snap stream through scheduler...",
                Assert.Single(run.Log.With("pivot header received")).Message);
        }

        [Fact]
        public async Task Given_AnEmptyHeaderResponse_When_ThePivotIsFetched_Then_ItBacksOffAndRetriesUntilTheHeaderArrives()
        {
            var header = new BlockHeader { BlockNumber = 936, StateRoot = CharBytes.Hash32(0x44), ParentHash = CharBytes.Hash32(0x45) };
            var calls = 0;
            var scheduler = new CharScheduler
            {
                Headers = (start, limit, ct) =>
                    Task.FromResult(++calls == 1 ? new List<BlockHeader>() : new List<BlockHeader> { header }),
            };
            using var run = new Started(new CharTipSource(Tip(1_000, withRoot: false)), scheduler);

            Assert.True(await run.Log.WaitForAsync("pivot header received", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            Assert.True(calls >= 2);
            Assert.False(run.Log.Saw("pivot header fetch threw"));
        }

        [Fact]
        public async Task Given_AHeaderFetchThatThrows_When_ThePivotIsFetched_Then_TheFailureIsLoggedAndTheFetchIsRetried()
        {
            var header = new BlockHeader { BlockNumber = 936, StateRoot = CharBytes.Hash32(0x44), ParentHash = CharBytes.Hash32(0x45) };
            var calls = 0;
            var scheduler = new CharScheduler
            {
                Headers = (start, limit, ct) => ++calls == 1
                    ? throw new InvalidOperationException("no peer available")
                    : Task.FromResult(new List<BlockHeader> { header }),
            };
            using var run = new Started(new CharTipSource(Tip(1_000, withRoot: false)), scheduler);

            Assert.True(await run.Log.WaitForAsync("pivot header received", TimeSpan.FromSeconds(10)));
            await run.StopAsync();

            var warn = Assert.Single(run.Log.With("pivot header fetch threw"));
            Assert.Equal(LogLevel.Warning, warn.Level);
            Assert.Equal("Snap-bootstrap: pivot header fetch threw; will retry", warn.Message);
            Assert.Equal("no peer available", warn.Exception.Message);
            Assert.True(calls >= 2);
        }

        [Fact]
        public async Task Given_NoCanonicalSourceAndNoSnapPeers_When_TheRunIsCancelledWhileWaitingForAPeerTip_Then_ItReturnsNotRanWithTheCancelledReason()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            using var cts = new CancellationTokenSource();
            var log = new CharLog();
            var run = SnapSyncOrchestrator.RunAsync(
                bundle, new CharPool(), new CharScheduler(), canonicalTip: null, activations: null, log,
                new SnapSyncOrchestratorOptions { RunHistoryBackfill = false }, cts.Token);

            await Task.Delay(200);
            Assert.False(run.IsCompleted);
            cts.Cancel();
            var result = await run;

            Assert.False(result.Ran);
            Assert.Equal("cancelled while fetching pivot", result.SkipReason);
        }

        [Fact]
        public async Task Given_AStoreAnchoredRefresherAndATipAtTheTrailDistance_When_Refreshed_Then_ItReturnsNothingAndLogsTheNoopReason()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var log = new CharLog();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), new CharScheduler(), new CharTipSource(Tip(Trail)), log, pivot, bundle);

            var fresh = await refresher(true, CancellationToken.None);

            Assert.Null(fresh);
            Assert.Equal(
                "snap.pivot.refresh_noop reason=no_canonical_tip_or_too_low",
                Assert.Single(log.Entries).Message);
        }

        [Fact]
        public async Task Given_AStoreAnchoredRefresherAndAPivotThatIsNotStale_When_RefreshedWithoutForce_Then_ItReturnsNothingAndLogsTheNoop()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var log = new CharLog();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 990 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), new CharScheduler(), new CharTipSource(Tip(1_000)), log, pivot, bundle);

            var fresh = await refresher(false, CancellationToken.None);

            Assert.Null(fresh);
            Assert.Equal(
                "snap.pivot.refresh_noop current=990 tip=1000 stale=false",
                Assert.Single(log.Entries).Message);
        }

        [Fact]
        public async Task Given_AStoreAnchoredRefresherAndAStalePivotWithNoTrailedHeaderLaid_When_Refreshed_Then_ItWarnsThatItIsWaitingForTheTrail()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var log = new CharLog();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 500 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), new CharScheduler(), new CharTipSource(Tip(1_000)), log, pivot, bundle);

            var fresh = await refresher(false, CancellationToken.None);

            Assert.Null(fresh);
            var warn = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Warning, warn.Level);
            Assert.Equal(
                "snap.pivot.refresh_waiting_for_trail current=500 tip=1000 reason=missing_trailed_header",
                warn.Message);
        }

        [Fact]
        public async Task Given_ATipOnlyRefresherAndATipAtTheTrailDistance_When_Refreshed_Then_ItReturnsNothingAndLogsTheNoopReason()
        {
            var log = new CharLog();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), new CharScheduler(), new CharTipSource(Tip(Trail)), log, pivot);

            Assert.Null(await refresher(false, CancellationToken.None));
            Assert.Equal(
                "snap.pivot.refresh_noop reason=no_canonical_tip_or_too_low",
                Assert.Single(log.Entries).Message);
        }

        [Fact]
        public async Task Given_ATipOnlyRefresherAndATipCarryingRootAndHash_When_Refreshed_Then_ItReturnsThePivotBuiltFromTheTipWithoutAskingPeers()
        {
            var peerAsked = false;
            var scheduler = new CharScheduler { Headers = (s, l, c) => { peerAsked = true; return Task.FromResult(new List<BlockHeader>()); } };
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), scheduler, new CharTipSource(Tip(1_000)), new CharLog(), pivot);

            var fresh = await refresher(false, CancellationToken.None);

            Assert.NotNull(fresh);
            Assert.Equal(1_000, (long)fresh.Value.Header.BlockNumber);
            Assert.Equal(CharBytes.Hash32(0x20), fresh.Value.Header.StateRoot);
            Assert.Equal(CharBytes.Hash32(0x10), fresh.Value.Hash);
            Assert.False(peerAsked);
        }

        [Fact]
        public async Task Given_ATipOnlyRefresherAndATipWithoutARoot_When_Refreshed_Then_ItFetchesTheHeaderAtTipMinusTheTrailDistanceFromThePeers()
        {
            var header = new BlockHeader { BlockNumber = 936, StateRoot = CharBytes.Hash32(0x44), ParentHash = CharBytes.Hash32(0x45) };
            ulong asked = 0;
            var scheduler = new CharScheduler
            {
                Headers = (start, limit, ct) => { asked = start; return Task.FromResult(new List<BlockHeader> { header }); },
            };
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), scheduler, new CharTipSource(Tip(1_000, withRoot: false)), new CharLog(), pivot);

            var fresh = await refresher(false, CancellationToken.None);

            Assert.Equal(1_000UL - Trail, asked);
            Assert.Same(header, fresh.Value.Header);
            Assert.Equal(RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header), fresh.Value.Hash);
        }

        [Fact]
        public async Task Given_ATipOnlyRefresherAndAnEmptyHeaderResponse_When_Refreshed_Then_ItReturnsNothing()
        {
            var scheduler = new CharScheduler { Headers = (s, l, c) => Task.FromResult(new List<BlockHeader>()) };
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), scheduler, new CharTipSource(Tip(1_000, withRoot: false)), new CharLog(), pivot);

            Assert.Null(await refresher(false, CancellationToken.None));
        }

        [Fact]
        public async Task Given_NoCanonicalSourceAndNoSnapPeersInThePool_When_Refreshed_Then_ItReturnsNothingWithoutFetching()
        {
            var fetched = false;
            var scheduler = new CharScheduler { Headers = (s, l, c) => { fetched = true; return Task.FromResult(new List<BlockHeader>()); } };
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(
                new CharPool(), scheduler, canonicalTip: null, new CharLog(), pivot);

            Assert.Null(await refresher(false, CancellationToken.None));
            Assert.False(fetched);
        }

        [Fact]
        public async Task Given_ATipSourceThatThrows_When_Refreshed_Then_ItWarnsWithTheExceptionTypeAndReturnsNothing()
        {
            var log = new CharLog();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var source = new CharTipSource(_ => throw new InvalidOperationException("rpc down"));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(new CharPool(), new CharScheduler(), source, log, pivot);

            Assert.Null(await refresher(false, CancellationToken.None));

            var warn = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Warning, warn.Level);
            Assert.Equal("snap.pivot.refresh_failed error_type=InvalidOperationException", warn.Message);
        }

        [Fact]
        public async Task Given_ARefreshCancelledByTheCaller_When_TheTipSourceObservesIt_Then_TheCancellationPropagatesInsteadOfBeingSwallowed()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var pivot = new SnapBootstrapper.RollingPivot(new BlockHeader { BlockNumber = 900 }, CharBytes.Hash32(0x01));
            var source = new CharTipSource(ct => Task.FromCanceled<CanonicalTip>(ct));
            var refresher = SnapSyncOrchestrator.BuildPivotRefresher(new CharPool(), new CharScheduler(), source, new CharLog(), pivot);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresher(false, cts.Token));
        }

        [Fact]
        public async Task Given_APeerHeaderFetchThatNeverAnswers_When_TheRunIsCancelled_Then_ItReturnsNotRanWithTheCancelledReasonInsteadOfThrowing()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            using var cts = new CancellationTokenSource();
            var log = new CharLog();
            var run = SnapSyncOrchestrator.RunAsync(
                bundle, new CharPool(), new CharScheduler(), new CharTipSource(Tip(1_000, withRoot: false)), activations: null, log,
                new SnapSyncOrchestratorOptions { UseBackwardSkeleton = false, RunHistoryBackfill = false }, cts.Token);

            await Task.Delay(200);
            Assert.False(run.IsCompleted);
            cts.Cancel();
            var result = await run;

            Assert.False(result.Ran);
            Assert.Equal("cancelled while fetching pivot", result.SkipReason);
            Assert.False(log.Saw("pivot header fetch threw"));
        }
    }
}
