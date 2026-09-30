using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Trait("Category", "Load")]
    [Collection(Snap2LiveHarness.Collection)]
    public class Snap2LiveRestartTests
    {
        private readonly ITestOutputHelper _out;
        public Snap2LiveRestartTests(ITestOutputHelper @out) => _out = @out;

        private sealed class Live : IAsyncDisposable
        {
            public Server Server { get; init; }
            public PeerPoolManager Pool { get; init; }
            public ControlledSnapScheduler Scheduler { get; init; }
            public string DbPath { get; init; }
            public RocksDbManager Manager { get; init; }
            public RocksDbChainStoreBundle Bundle { get; init; }

            public static async Task<Live> StartAsync()
            {
                var server = await StartServerWithInitialGapAsync(preFollowerBlocks: 80);
                var (pool, scheduler) = await ConnectAsync(server.Node);
                var dbPath = Path.Combine(Path.GetTempPath(), "snap2-live-restart-" + Guid.NewGuid().ToString("N"));
                var (manager, bundle) = OpenFollowerBundle(dbPath);
                return new Live
                {
                    Server = server, Pool = pool, Scheduler = new ControlledSnapScheduler(scheduler),
                    DbPath = dbPath, Manager = manager, Bundle = bundle,
                };
            }

            public Task<SnapBootstrapper.Result> RunAsync(CapturingLogger log, SnapSyncMetrics metrics, CancellationToken ct)
                => Follower(Bundle, Server.Sequencer, log, Pool, Scheduler)
                    .RunSnapBootstrapAsync(new LiveAdvancingTipSource(Server.Node), Snap2Options(metrics), ct);

            public async ValueTask DisposeAsync()
            {
                await Pool.DisposeAsync();
                Bundle.Dispose();
                await Server.Node.DisposeAsync();
                try { Directory.Delete(DbPath, true); } catch { }
            }
        }

        private static async Task LayServerHeadersAsync(Live live)
        {
            var laid = HeaderSubchains.TrustedTip(live.Bundle.Metadata.GetHeaderSyncState());
            var tip = await live.Server.HeightAsync();
            for (var n = laid + 1; n <= tip; n++)
            {
                var header = await live.Server.Node.Bundle.Blocks.GetByNumberAsync(n);
                var hash = await live.Server.Node.Bundle.Blocks.GetHashByNumberAsync(n);
                await live.Bundle.Blocks.SaveAsync(header, hash);
            }
            live.Bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(live.Bundle.Metadata.GetHeaderSyncState(), tip));
        }

        private static ulong? LastCaughtUpBlock(CapturingLogger log)
            => log.Messages.Where(m => m.Contains("snap.bal_catchup.applied", StringComparison.Ordinal))
                .Select(m => ulong.Parse(m.Substring(m.IndexOf("..", StringComparison.Ordinal) + 2).Split(' ')[0]))
                .Cast<ulong?>()
                .LastOrDefault();

        private async Task StopAfterAConvergedPivotMoveAsync(Live live, CapturingLogger log)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            var run = live.RunAsync(log, new SnapSyncMetrics(), cts.Token);

            Assert.True(
                await WaitUntilAsync(() => log.Count("snap.verify") > 0, TimeSpan.FromSeconds(30)),
                "phase 2 never started streaming");
            live.Scheduler.PauseStateRequests();
            while (log.Count("pivot_move.bal_healed") == 0 && !run.IsCompleted && await live.Server.HeightAsync() < 400)
            {
                await live.Server.SealAsync(10);
                await Task.WhenAny(run, Task.Delay(TimeSpan.FromMilliseconds(TestRootRefreshIntervalMs * 3)));
            }
            var trailedTip = await live.Server.HeightAsync() - SnapSyncOrchestrator.PivotServeTrailDistance;
            Assert.True(
                await WaitUntilAsync(() => LastCaughtUpBlock(log) == trailedTip || run.IsCompleted, TimeSpan.FromSeconds(30)),
                $"the catch-up never converged on the trailed tip {trailedTip} (last applied {LastCaughtUpBlock(log)})");
            Assert.False(run.IsCompleted, "the first run finished before it could be stopped");

            cts.Cancel();
            var stopped = await run;
            Assert.False(stopped.Ran);
            live.Scheduler.ResumeStateRequests();
        }

        [Fact]
        public async Task Given_ASnap2FollowerStoppedAfterAPivotMove_When_RestartedAfterTheTipAdvancedPast128Blocks_Then_ResumeCatchUpClosesTheGapAndTheGeneratedRootMatches()
        {
            await using var live = await Live.StartAsync();
            var log1 = new CapturingLogger();
            var log2 = new CapturingLogger();
            try
            {
                await StopAfterAConvergedPivotMoveAsync(live, log1);
                var saved = live.Bundle.Metadata.GetSnapSyncState();
                Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
                Assert.NotEmpty(saved.Tasks);

                var stoppedHeight = await live.Server.HeightAsync();
                while (await live.Server.HeightAsync() < stoppedHeight + 130)
                    await live.Server.SealAsync(10);
                await LayServerHeadersAsync(live);

                var metrics = new SnapSyncMetrics();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var result = await live.RunAsync(log2, metrics, cts.Token);

                Assert.True(result.Ran, result.SkipReason);
                Assert.True(log2.Count("snap.bootstrap.snap2_resume phase=Phase2Running") > 0, "the restart did not resume the saved snap/2 download");
                Assert.Equal(0, log2.Count("snap.bootstrap.snap2_reset"));
                var messages = log2.Messages.ToList();
                var firstCatchUp = messages.FindIndex(m => m.Contains("snap.bal_catchup.applied", StringComparison.Ordinal));
                var firstRange = messages.FindIndex(m => m.Contains("snap.verify", StringComparison.Ordinal));
                Assert.True(firstCatchUp >= 0 && (firstRange < 0 || firstCatchUp < firstRange), "the resume did not catch up before Phase 2 resumed");
                Assert.Contains($"range={saved.PivotBlockNumber + 1}..", messages[firstCatchUp]);
                Assert.True(metrics.BalHealBlocksAppliedTotal >= 128, $"only {metrics.BalHealBlocksAppliedTotal} BAL blocks applied on resume");
                Assert.Equal(0L, metrics.Phase3NodesHealedTotal);

                var pivotHeader = await live.Server.Node.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
                Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(log2));
                await AssertFlatStateCertifiedAsync(live.Bundle, live.Manager, result.PivotStateRoot);
                await result.HistoryBackfill;
            }
            catch
            {
                foreach (var m in log1.Messages.Concat(log2.Messages)) _out.WriteLine(m);
                throw;
            }
        }

        [Fact]
        public async Task Given_ASnap2FollowerStoppedAfterAPivotMove_When_RestartedWithNoTipMovement_Then_ResumeAppliesNoBal()
        {
            await using var live = await Live.StartAsync();
            var log1 = new CapturingLogger();
            var log2 = new CapturingLogger();
            try
            {
                await StopAfterAConvergedPivotMoveAsync(live, log1);
                var saved = live.Bundle.Metadata.GetSnapSyncState();

                var metrics = new SnapSyncMetrics();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var result = await live.RunAsync(log2, metrics, cts.Token);

                Assert.True(result.Ran, result.SkipReason);
                Assert.True(log2.Count("snap.bootstrap.snap2_resume phase=Phase2Running") > 0, "the restart did not resume the saved snap/2 download");
                Assert.Equal(0L, metrics.BalHealBlocksAppliedTotal);
                Assert.Equal(saved.PivotBlockNumber, result.PivotBlockNumber);
                var pivotHeader = await live.Server.Node.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
                await result.HistoryBackfill;
            }
            catch
            {
                foreach (var m in log1.Messages.Concat(log2.Messages)) _out.WriteLine(m);
                throw;
            }
        }

        [Fact]
        public async Task Given_ASnap2FollowerStoppedDuringGeneration_When_Restarted_Then_ItRegeneratesAgainstTheFrozenPivotWithoutPhase2()
        {
            await using var live = await Live.StartAsync();
            var log1 = new CapturingLogger();
            var log2 = new CapturingLogger();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180)))
                {
                    log1.OnMessage = m =>
                    {
                        if (m.StartsWith("snap.generate.start", StringComparison.Ordinal)) cts.Cancel();
                    };
                    var stopped = await live.RunAsync(log1, new SnapSyncMetrics(), cts.Token);
                    Assert.False(stopped.Ran);
                }
                var frozen = live.Bundle.Metadata.GetSnapSyncState();
                Assert.Equal(SnapPhase.Generating, frozen.Phase);

                await live.Server.SealAsync(20);

                var metrics = new SnapSyncMetrics();
                using var restart = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var result = await live.RunAsync(log2, metrics, restart.Token);

                Assert.True(result.Ran, result.SkipReason);
                Assert.True(log2.Count("snap.bootstrap.snap2_resume phase=Generating") > 0, "the restart did not resume generation");
                Assert.Equal(0, log2.Count("snap.verify"));
                Assert.Equal(0, log2.Count("snap.phase2"));
                Assert.Equal(0L, metrics.BalHealBlocksAppliedTotal);
                Assert.Equal(frozen.PivotBlockNumber, result.PivotBlockNumber);
                var pivotHeader = await live.Server.Node.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
                Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(log2));
                await AssertFlatStateCertifiedAsync(live.Bundle, live.Manager, result.PivotStateRoot);
                await result.HistoryBackfill;
            }
            catch
            {
                foreach (var m in log1.Messages.Concat(log2.Messages)) _out.WriteLine(m);
                throw;
            }
        }
    }
}
