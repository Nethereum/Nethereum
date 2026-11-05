using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Xunit;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    public partial class WireHealTests
    {
        private const string CertifyDone = "snap.flat.reconcile done";
        private const string StreamPopulated = "Snap-bootstrap: state populated";

        private static List<(byte[] Key, byte[] Value)> JournalRows(RocksDbManager manager)
        {
            var rows = new List<(byte[] Key, byte[] Value)>();
            using var it = manager.CreateIterator(RocksDbManager.CF_FLAT_GENERATION);
            for (it.SeekToFirst(); it.Valid(); it.Next()) rows.Add((it.Key(), it.Value()));
            return rows;
        }

        private static async Task<string> CopyWithAFullJournalAsync(Live live)
        {
            var copy = live.DbPath + "-certified";
            live.NewLog();
            await using var db = live.Open();
            using var cts = new CancellationTokenSource(RunTimeout);
            var captured = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            live.On(m =>
            {
                if (captured.Task.IsCompleted || !StartsWith(m, CertifyDone)) return;
                db.Manager.CreateDatabaseCheckpoint(copy);
                cts.Cancel();
                captured.TrySetResult(true);
            });
            var run = live.RunAsync(db, cts.Token);
            Assert.True(await Task.WhenAny(captured.Task, Task.Delay(RunTimeout)) == captured.Task, "the certify pass never completed");
            await StopAsync(run);
            return copy;
        }

        [Fact]
        public Task Given_ALiveJournalAndAResumeThatReEntersPhase2_When_TheBootstrapStamps_Then_TheJournalIsDiscarded()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false), staticTipAtHead: true);
                var copy = await CopyWithAFullJournalAsync(live);
                try
                {
                    await using var resumed = OpenFollowerAt(copy);
                    var saved = resumed.Bundle.Metadata.GetSnapSyncState();
                    var journal = JournalRows(resumed.Manager);
                    Assert.NotEmpty(journal);
                    resumed.Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                    {
                        SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                        Phase = SnapPhase.Phase2Running,
                        PivotBlockNumber = saved.PivotBlockNumber,
                        PivotBlockHash = saved.PivotBlockHash,
                        HealTargetRoot = new byte[32],
                        Tasks = saved.Tasks,
                        Counters = saved.Counters,
                    });
                    foreach (var (key, value) in journal) resumed.Manager.Put(RocksDbManager.CF_FLAT_GENERATION, key, value);
                    Assert.Equal(journal.Count, JournalRows(resumed.Manager).Count);

                    var log = live.NewLog();
                    var unitsDoneWhenPhase2Ended = -1;
                    live.On(m =>
                    {
                        if (StartsWith(m, StreamPopulated) && unitsDoneWhenPhase2Ended < 0)
                            unitsDoneWhenPhase2Ended = UnitsDone(resumed.Manager);
                    });
                    using var cts = new CancellationTokenSource(RunTimeout);

                    var result = await CompletedAsync(live.RunAsync(resumed, cts.Token));

                    Assert.True(log.Count("snap.bootstrap.resume phase=Phase2") > 0, "the run did not resume Phase 2 from the written state");
                    Assert.Equal(0, unitsDoneWhenPhase2Ended);
                    await AssertSyncedStateAsync(live, resumed.Bundle, result, cts.Token, minPivot: 0);
                    Assert.Empty(JournalRows(resumed.Manager));
                }
                finally
                {
                    try { if (Directory.Exists(copy)) Directory.Delete(copy, true); } catch { }
                }
            });

        [Fact]
        public Task Given_ALiveJournal_When_ThePivotCommits_Then_NoJournalSurvivesTheBootstrap()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false), staticTipAtHead: true);
                live.NewLog();
                var unitsDoneWhenCertifyEnded = -1;
                await using (var interrupted = live.Open())
                {
                    using var interruptedCts = new CancellationTokenSource(RunTimeout);
                    live.On(m =>
                    {
                        if (!StartsWith(m, CertifyDone) || unitsDoneWhenCertifyEnded >= 0) return;
                        unitsDoneWhenCertifyEnded = UnitsDone(interrupted.Manager);
                        interruptedCts.Cancel();
                    });

                    await StopAsync(live.RunAsync(interrupted, interruptedCts.Token));

                    Assert.True(unitsDoneWhenCertifyEnded > 0, "the certify journal was not live before the pivot committed");
                    Assert.Equal(0UL, interrupted.Bundle.Metadata.GetLastBlock());
                    Assert.Empty(JournalRows(interrupted.Manager));
                }

                live.NewLog();
                await using var db = live.Open();
                using var cts = new CancellationTokenSource(RunTimeout);

                var result = await CompletedAsync(live.RunAsync(db, cts.Token));

                await AssertSyncedStateAsync(live, db.Bundle, result, cts.Token, minPivot: 0);
                Assert.Empty(JournalRows(db.Manager));
            });

        [Fact]
        public Task Given_AJournalDiscardedBeforeTheFinalCommitAndTheBatchNeverCommitted_When_TheNodeRestartsAndResumes_Then_CertifyRunsFromScratchAndTheBootstrapCompletes()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false), staticTipAtHead: true);
                var copy = await CopyWithAFullJournalAsync(live);
                try
                {
                    await using (var crashed = OpenFollowerAt(copy))
                    {
                        Assert.NotEmpty(JournalRows(crashed.Manager));
                        ((IFlatStateReconciler)crashed.Bundle).DiscardCertifyJournal();
                    }
                    await using var resumed = OpenFollowerAt(copy);
                    Assert.Empty(JournalRows(resumed.Manager));
                    Assert.Equal(SnapPhase.Phase3Running, resumed.Bundle.Metadata.GetSnapSyncState().Phase);
                    var log = live.NewLog();
                    using var cts = new CancellationTokenSource(RunTimeout);

                    var result = await CompletedAsync(live.RunAsync(resumed, cts.Token));

                    Assert.Equal(0, UnitsDoneAtGenerationStart(log));
                    await AssertSyncedStateAsync(live, resumed.Bundle, result, cts.Token, minPivot: 0);
                    Assert.Empty(JournalRows(resumed.Manager));
                }
                finally
                {
                    try { if (Directory.Exists(copy)) Directory.Delete(copy, true); } catch { }
                }
            });
    }
}
