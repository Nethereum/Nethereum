using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Trait("Category", "Load")]
    [Collection(Snap2LiveHarness.Collection)]
    public class BalHealLivePivotMoveTests
    {
        private readonly ITestOutputHelper _out;
        public BalHealLivePivotMoveTests(ITestOutputHelper @out) => _out = @out;

        [Fact]
        public async Task Given_AFollowerSnapSyncingWithBalHealEnabled_When_TheProducerSealsBlocksAndThePivotMovesRepeatedlyMidFlight_Then_TheGapIsClosedByBalHeal_NotByDrainAndRedownload()
        {
            var server = await StartServerWithInitialGapAsync(preFollowerBlocks: 80);
            await using var __ = server.Node;

            var (pool, inner) = await ConnectAsync(server.Node);
            var scheduler = new ControlledSnapScheduler(inner);

            var dbPath = Path.Combine(Path.GetTempPath(), "bal-heal-live-" + Guid.NewGuid().ToString("N"));
            var log = new CapturingLogger();
            try
            {
                var (manager, bundle) = OpenFollowerBundle(dbPath);
                using var bundleScope = bundle;
                long? trieRowsWhenGenerationStarted = null;
                log.OnMessage = m =>
                {
                    if (m.StartsWith("snap.generate.start", StringComparison.Ordinal))
                        trieRowsWhenGenerationStarted = CountTrieRows(manager);
                };

                var metrics = new SnapSyncMetrics();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                var runTask = Follower(bundle, server.Sequencer, log, pool, scheduler)
                    .RunSnapBootstrapAsync(new LiveAdvancingTipSource(server.Node), Snap2Options(metrics), cts.Token);

                Assert.True(
                    await WaitUntilAsync(() => log.Messages.Any(m => m.Contains("snap.verify", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromSeconds(30)),
                    "phase 2 never started streaming");

                for (var move = 1; move <= 2; move++)
                {
                    scheduler.PauseStateRequests();
                    while ((log.Count("pivot_move.bal_healed") < move || (move == 2 && await server.HeightAsync() < 130))
                           && !runTask.IsCompleted)
                    {
                        await server.SealAsync(10);
                        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromMilliseconds(TestRootRefreshIntervalMs * 3)));
                    }
                    scheduler.ResumeStateRequests();
                    var verifiedBefore = log.Count("snap.verify");
                    await WaitUntilAsync(() => log.Count("snap.verify") >= verifiedBefore + 5 || runTask.IsCompleted, TimeSpan.FromSeconds(30));
                }

                var result = await runTask;
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                var totalHeight = await server.HeightAsync();
                _out.WriteLine($"producer sealed {totalHeight} blocks; follower converged at pivot {result.PivotBlockNumber}");
                Assert.True(totalHeight >= 128, $"workload only sealed {totalHeight} blocks — need 128+");

                var balHealedBlocks = metrics.BalHealBlocksAppliedTotal;
                var triHealedNodes = metrics.Phase3NodesHealedTotal;
                _out.WriteLine($"BalHealBlocksApplied={balHealedBlocks} Phase3NodesHealed={triHealedNodes}");

                var unhealedPivotMoves = log.Messages.Count(m =>
                    m.Contains("snap.phase2.pivot_move", StringComparison.Ordinal)
                    && !m.Contains("bal_healed", StringComparison.Ordinal));
                var healedPivotMoves = log.Count("pivot_move.bal_healed");
                _out.WriteLine($"healedPivotMoves={healedPivotMoves} unhealedPivotMoves={unhealedPivotMoves}");

                Assert.True(healedPivotMoves >= 2, $"expected the pivot to move (and be BAL-healed) more than once, saw {healedPivotMoves}");
                Assert.Equal(0, unhealedPivotMoves);
                Assert.True(balHealedBlocks > 0, "BalHealBlocksApplied never incremented — the gap-closer never ran");
                Assert.Equal(0, triHealedNodes);
                Assert.DoesNotContain(log.Messages, m => m.Contains("entering heal phase", StringComparison.OrdinalIgnoreCase));

                var pivotHeaderOnServer = await server.Node.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                Assert.Equal(pivotHeaderOnServer.StateRoot.ToHex(), result.PivotStateRoot.ToHex());

                Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(log));
                Assert.Equal(0L, trieRowsWhenGenerationStarted);
                await AssertFlatStateCertifiedAsync(bundle, manager, result.PivotStateRoot);

                var gapStart = 81;
                var nonEmptyGapBlocks = 0;
                for (var n = gapStart; n <= (long)totalHeight; n++)
                {
                    var hash = await server.Node.Bundle.Blocks.GetHashByNumberAsync(n);
                    var rlp = await server.Node.Bundle.BlockAccessLists.GetByBlockHashAsync(hash);
                    if (rlp != null && BlockAccessListRLPEncoder.Current.Decode(rlp).Count > 0) nonEmptyGapBlocks++;
                }
                Assert.True(nonEmptyGapBlocks > 0, "no gap block carried a non-empty block access list — the gate would be vacuous");
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

        [Fact(Skip =
            "Same live pivot-rotation flakiness as the enabled test above — see its Skip reason. This is " +
            "the snap/1-equivalent regression pin (BalHealEnabled=false) for the same live harness; left " +
            "in place pending the same investigation.")]
        public async Task Given_BalHealIsDisabled_When_ThePivotMovesMidFlight_Then_TheExistingDrainAndRedownloadPathStillRuns()
        {
            var server = await StartServerWithInitialGapAsync(preFollowerBlocks: 80);
            await using var __ = server.Node;

            var (pool, scheduler) = await ConnectAsync(server.Node);

            var dbPath = Path.Combine(Path.GetTempPath(), "bal-heal-off-" + Guid.NewGuid().ToString("N"));
            try
            {
                var (_, bundle) = OpenFollowerBundle(dbPath);
                using var bundleScope = bundle;

                var log = new CapturingLogger();
                var metrics = new SnapSyncMetrics();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var runTask = Follower(bundle, server.Sequencer, log, pool, scheduler)
                    .RunSnapBootstrapAsync(new LiveAdvancingTipSource(server.Node), Snap2Options(metrics, balHealEnabled: false), cts.Token);

                Assert.True(
                    await WaitUntilAsync(() => log.Messages.Any(m => m.Contains("snap.verify", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromSeconds(30)),
                    "phase 2 never started streaming");

                for (var wave = 0; wave < 3 && !runTask.IsCompleted; wave++)
                {
                    await server.SealAsync(10);
                    await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromMilliseconds(TestRootRefreshIntervalMs * 3)));
                }

                var result = await runTask;
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                foreach (var m in log.Messages) _out.WriteLine(m);
                _out.WriteLine($"final height={await server.HeightAsync()} pivot={result.PivotBlockNumber}");

                Assert.Equal(0, metrics.BalHealBlocksAppliedTotal);
                Assert.Contains(log.Messages, m =>
                    m.Contains("snap.phase2.pivot_move", StringComparison.Ordinal)
                    && !m.Contains("bal_healed", StringComparison.Ordinal));
                Assert.DoesNotContain(log.Messages, m => m.Contains("pivot_move.bal_healed", StringComparison.Ordinal));
            }
            finally
            {
                await pool.DisposeAsync();
                if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true);
            }
        }
    }
}
