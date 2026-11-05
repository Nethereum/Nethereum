using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    public partial class WireHealTests
    {
        private static readonly TimeSpan PastTheHealersPivotCheck = TimeSpan.FromSeconds(14);
        private const string HealEntry = "snap.phase.transition from=Phase2 to=Phase3";
        private const string Converged = "snap.phase3.converged";
        private const string ReconcileStart = "snap.flat.reconcile starting root=0x";

        private static List<string> RootsAfter(CapturingLogger log, string marker)
            => log.Messages.Where(m => m.Contains(marker, StringComparison.Ordinal))
                .Select(m => RootAfter(m, marker).ToHex())
                .ToList();

        [Theory]
        [Trait("Reproducer", "nuc-2026-10-07-certify-at-the-rolling-pivot")]
        [InlineData(true)]
        [InlineData(false)]
        public Task Given_APivotThatMayAdvanceFewerThan120BlocksDuringTheHealTail_When_TheHealConverges_Then_TheStorageHealCertifyAndCommittedPivotAllUseTheConvergedRoot(bool pivotAdvancesDuringTheHeal)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false));
                var log = live.NewLog();
                var healEntered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                live.On(m =>
                {
                    if (!StartsWith(m, HealEntry) || !healEntered.TrySetResult(m) || !pivotAdvancesDuringTheHeal) return;
                    live.Scheduler.HoldTrieNodeRequests();
                    live.AdvanceStage();
                });
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 2);
                    Assert.True(await Task.WhenAny(healEntered.Task, Task.Delay(StepTimeout)) == healEntered.Task,
                        "phase 2 never handed off to a trie-node heal");
                    if (pivotAdvancesDuringTheHeal)
                    {
                        await Task.Delay(PastTheHealersPivotCheck);
                        live.Scheduler.ReleaseTrieNodeRequests();
                    }
                    var result = await CompletedAsync(run);

                    var healRoot = RootAfter(healEntered.Task.Result, "heal_target=0x").ToHex();
                    var reconcileRoots = RootsAfter(log, ReconcileStart);
                    Assert.NotEmpty(reconcileRoots);
                    Assert.All(reconcileRoots, root => Assert.Equal(healRoot, root));
                    Assert.Equal(0, log.Count("snap.flat.reheal attempt="));
                    Assert.Equal(0, log.Count("snap.flat.certify.guard"));
                    Assert.Equal(healRoot, result.PivotStateRoot.ToHex());

                    var converged = Assert.Single(log.Messages, m => m.Contains(Converged, StringComparison.Ordinal));
                    var rollingRoot = RootAfter(converged, "rolling_pivot_root=0x").ToHex();
                    if (pivotAdvancesDuringTheHeal)
                        Assert.NotEqual(healRoot, rollingRoot);
                    else
                        Assert.Equal(healRoot, rollingRoot);
                    await AssertSyncedStateAsync(live, bundle, result, cts.Token, minPivot: 0);
                }
            });

        [Fact]
        [Trait("Reproducer", "nuc-2026-10-07-heal-beheads-the-stored-trie")]
        public Task Given_AnUnseededHealAtARootNoPeerServes_When_ItWaitsForANewPivot_Then_TheStoredAccountRootIsNotDeletedAndHealingAServedRootReplacesIt()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static));
                var log = live.NewLog();
                byte[] unservedRoot = null;
                byte[] storedRootAtHealEntry = null;
                RocksDbManager manager = null;
                var phase3Entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                live.On(m =>
                {
                    if (!StartsWith(m, HealEntry)) return;
                    if (Interlocked.CompareExchange(ref unservedRoot, RootAfter(m, "heal_target=0x"), null) == null)
                        storedRootAtHealEntry = Volatile.Read(ref manager)?.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
                    phase3Entered.TrySetResult(m);
                });
                live.Scheduler.WithholdTrieNodes((root, _) =>
                {
                    var unserved = Volatile.Read(ref unservedRoot);
                    return unserved != null && ByteUtil.AreEqual(root, unserved);
                });

                await using (var bundleDb = live.Open())
                {
                    Volatile.Write(ref manager, bundleDb.Manager);
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 1);
                    Assert.True(await Task.WhenAny(phase3Entered.Task, Task.Delay(StepTimeout)) == phase3Entered.Task,
                        "phase 2 never handed off to a trie-node heal");
                    Assert.True(await WaitUntilAsync(() => log.Count(WaitingForPivot) > 0, StepTimeout),
                        "the heal never started waiting on the root no peer serves");
                    Assert.True(live.Scheduler.WithheldTrieNodePaths > 0, "the heal never asked for the unserved root");
                    Assert.NotNull(storedRootAtHealEntry);

                    var storedWhileWaiting = bundleDb.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
                    Assert.True(storedWhileWaiting != null, "a heal against a root no peer serves deleted the stored account root before fetching anything");
                    Assert.Equal(storedRootAtHealEntry.ToHex(), storedWhileWaiting.ToHex());

                    live.AdvanceStage();
                    var result = await CompletedAsync(run);
                    var storedAfter = bundleDb.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
                    Assert.NotNull(storedAfter);
                    Assert.Equal(result.PivotStateRoot.ToHex(), Sha3Keccack.Current.CalculateHash(storedAfter).ToHex());
                    Assert.NotEqual(unservedRoot.ToHex(), result.PivotStateRoot.ToHex());
                }
            });

        [Fact]
        public Task Given_AnUnseededHealWhoseTargetRootIsServedButWhoseChildrenAreWithheld_When_ItWaitsForANewPivot_Then_TheStaleStoredRootWasDeletedOnceTheTargetRootWasFetched()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static));
                var log = live.NewLog();
                byte[] healTarget = null;
                byte[] storedRootAtHealEntry = null;
                RocksDbManager manager = null;
                var phase3Entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                live.On(m =>
                {
                    if (!StartsWith(m, HealEntry)) return;
                    if (Interlocked.CompareExchange(ref healTarget, RootAfter(m, "heal_target=0x"), null) == null)
                        storedRootAtHealEntry = Volatile.Read(ref manager)?.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
                    phase3Entered.TrySetResult(m);
                });
                live.Scheduler.WithholdTrieNodes((root, pathset) =>
                {
                    var target = Volatile.Read(ref healTarget);
                    return target != null && ByteUtil.AreEqual(root, target) && FirstAccountNibble(pathset) >= 0;
                });

                await using (var bundleDb = live.Open())
                {
                    Volatile.Write(ref manager, bundleDb.Manager);
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 1);
                    Assert.True(await Task.WhenAny(phase3Entered.Task, Task.Delay(StepTimeout)) == phase3Entered.Task,
                        "phase 2 never handed off to a trie-node heal");
                    Assert.True(await WaitUntilAsync(() => live.Scheduler.WithheldTrieNodePaths > 0, StepTimeout),
                        "the heal never asked for the children of the fetched target root");
                    Assert.NotNull(storedRootAtHealEntry);
                    Assert.NotEqual(healTarget.ToHex(), Sha3Keccack.Current.CalculateHash(storedRootAtHealEntry).ToHex());

                    Assert.Null(bundleDb.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>()));

                    live.Scheduler.ServeAllTrieNodes();
                    live.AdvanceStage();
                    var result = await CompletedAsync(run);
                    Assert.Equal(result.PivotStateRoot.ToHex(),
                        Sha3Keccack.Current.CalculateHash(bundleDb.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>())).ToHex());
                }
            });

        [Fact]
        [Trait("Reproducer", "nuc-2026-10-07-resume-from-a-headless-trie")]
        public Task Given_AStoppedHealWhoseAccountTrieLostItsTopTwoLevelsAndAPersistedWholeTrieSentinel_When_TheNodeRestarts_Then_ItHealsAtAFreshPivotCertifiesThatRootAndCommitsIt()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static));
                var log1 = live.NewLog();
                SnapSyncState saved;
                await using (var bundle1Db = live.Open())
                {
                    var (_, bundle1) = bundle1Db;
                    await bundle1.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    live.Scheduler.HoldTrieNodeRequests();
                    var run1 = live.RunAsync(bundle1Db, cts.Token);
                    await ForceMovesAsync(live, log1, 1);
                    Assert.True(
                        await WaitUntilAsync(() => log1.Count(HealEntry) > 0 && live.Scheduler.TrieNodeRequests > 0, StepTimeout),
                        "phase 2 never handed off to a trie-node heal");
                    cts.Cancel();
                    var stopped = await StopAsync(run1);
                    Assert.False(stopped?.Ran ?? false, "the heal finished before it could be stopped");
                    saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase3Running, saved.Phase);
                }

                await using (var offlineDb = live.Open())
                {
                    var (manager, bundle) = offlineDb;
                    var beheaded = BeheadAccountTrie(manager);
                    _out.WriteLine($"beheaded the account trie: {beheaded} node(s) at depth 0 and 1 deleted");
                    Assert.True(beheaded > 1, "the stopped heal left no top of the account trie to delete");
                    await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                        () => bundle.ReconcileFlatStateAsync(saved.HealTargetRoot, null, CancellationToken.None));
                    manager.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.ASCII.GetBytes("flatrepair:inventory"),
                        FlatReconcileUnresolvableNodeException.WholeAccountTrieSentinel.Concat(saved.HealTargetRoot).ToArray());
                    Assert.Contains(bundle.GetPersistedDamage(), d => ByteUtil.AreEqual(d.AccountHash, FlatReconcileUnresolvableNodeException.WholeAccountTrieSentinel));
                }

                live.Scheduler.ReleaseTrieNodeRequests();
                var log2 = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    var (_, bundle2) = bundle2Db;
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var result = await CompletedAsync(live.RunAsync(bundle2Db, cts.Token));

                    var convergedRoot = RootAfter(Assert.Single(log2.Messages, m => m.Contains(Converged, StringComparison.Ordinal)), " root=0x").ToHex();
                    Assert.All(RootsAfter(log2, ReconcileStart), root => Assert.Equal(convergedRoot, root));
                    Assert.Equal(0, log2.Count("snap.flat.reheal attempt="));
                    Assert.Equal(convergedRoot, result.PivotStateRoot.ToHex());
                    await AssertSyncedStateAsync(live, bundle2, result, cts.Token, minPivot: 0);
                }
            });

        private static int BeheadAccountTrie(RocksDbManager manager)
        {
            var top = new List<byte[]> { Array.Empty<byte>() };
            for (byte nibble = 0; nibble < 16; nibble++) top.Add(new[] { nibble });
            var deleted = 0;
            foreach (var path in top)
            {
                if (manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path) == null) continue;
                manager.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
                deleted++;
            }
            return deleted;
        }
    }
}
