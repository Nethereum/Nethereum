using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Trait("Category", "Load")]
    [Collection(Snap2LiveHarness.Collection)]
    public class Snap2LiveNoTrieNodesTests
    {
        private readonly ITestOutputHelper _out;
        public Snap2LiveNoTrieNodesTests(ITestOutputHelper @out) => _out = @out;

        [Fact]
        public async Task Given_Snap2OnlyPeersAndAWhaleWhosePagesSpanAMove_When_SyncCompletesThroughGeneration_Then_ZeroGetTrieNodesRequestsWereIssued()
        {
            var server = await StartServerWithInitialGapAsync(preFollowerBlocks: 80);
            await using var __ = server.Node;
            var (pool, inner) = await ConnectAsync(server.Node, advertiseSnap2: true);
            var scheduler = new ControlledSnapScheduler(inner, holdAWhaleAcrossAMove: true);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap2-live-notrie-" + Guid.NewGuid().ToString("N"));
            var log = new CapturingLogger();
            try
            {
                var (manager, bundle) = OpenFollowerBundle(dbPath);
                using var bundleScope = bundle;
                var metrics = new SnapSyncMetrics();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var run = Follower(bundle, server.Sequencer, log, pool, scheduler)
                    .RunSnapBootstrapAsync(new LiveAdvancingTipSource(server.Node), Snap2Options(metrics), cts.Token);

                while (!run.IsCompleted && await server.HeightAsync() < 400)
                {
                    await server.SealAsync(10);
                    await Task.WhenAny(run, Task.Delay(TimeSpan.FromMilliseconds(TestRootRefreshIntervalMs * 3)));
                }
                var result = await run;

                Assert.True(result.Ran, result.SkipReason);
                Assert.True(scheduler.WhaleRoots.Count >= 2, $"the whale {scheduler.Whale} was paged at {scheduler.WhaleRoots.Count} root(s); its pages must span a move");
                Assert.True(log.Count("pivot_move.bal_healed") >= 1, "no pivot move was caught up");
                Assert.Equal(0, scheduler.TrieNodeRequests);
                Assert.Equal(0L, metrics.Phase3NodesHealedTotal);
                Assert.Equal(0, log.Count("snap.phase2.bigaccount.deferred"));
                var pivotHeader = await server.Node.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
                Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(log));
                await AssertFlatStateCertifiedAsync(bundle, manager, result.PivotStateRoot);
                await result.HistoryBackfill;
            }
            catch
            {
                foreach (var m in log.Messages) _out.WriteLine(m);
                throw;
            }
            finally
            {
                await pool.DisposeAsync();
                if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task Given_Snap1PeersAndAWhaleWhosePagesSpanAMove_When_TheFollowerSyncs_Then_GetTrieNodesRequestsAreIssued()
        {
            var server = await StartServerWithInitialGapAsync(preFollowerBlocks: 80);
            await using var __ = server.Node;
            var (pool, inner) = await ConnectAsync(server.Node, advertiseSnap2: false);
            var scheduler = new ControlledSnapScheduler(inner, holdAWhaleAcrossAMove: true);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap1-live-trie-" + Guid.NewGuid().ToString("N"));
            var log = new CapturingLogger();
            try
            {
                var (_, bundle) = OpenFollowerBundle(dbPath);
                using var bundleScope = bundle;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var run = Follower(bundle, server.Sequencer, log, pool, scheduler)
                    .RunSnapBootstrapAsync(new LiveAdvancingTipSource(server.Node), Snap2Options(new SnapSyncMetrics(), balHealEnabled: false), cts.Token);

                while (!run.IsCompleted && scheduler.TrieNodeRequests == 0 && await server.HeightAsync() < 400)
                {
                    await server.SealAsync(10);
                    await Task.WhenAny(run, Task.Delay(TimeSpan.FromMilliseconds(TestRootRefreshIntervalMs * 3)));
                }
                await WaitUntilAsync(() => run.IsCompleted || scheduler.TrieNodeRequests > 0, TimeSpan.FromSeconds(60));
                cts.Cancel();
                try { await run; } catch (OperationCanceledException) { }

                Assert.True(scheduler.WhaleRoots.Count >= 2, $"the whale {scheduler.Whale} was paged at {scheduler.WhaleRoots.Count} root(s); its pages must span a move");
                Assert.True(scheduler.TrieNodeRequests > 0, "snap/1 healed without a single GetTrieNodes request");
            }
            catch
            {
                foreach (var m in log.Messages) _out.WriteLine(m);
                throw;
            }
            finally
            {
                await pool.DisposeAsync();
                if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true);
            }
        }
    }
}
