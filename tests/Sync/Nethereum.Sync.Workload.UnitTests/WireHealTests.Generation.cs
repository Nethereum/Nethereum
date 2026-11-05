using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Util;
using Xunit;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    public partial class WireHealTests
    {
        private static byte[] OwnerOf(string address) => Sha3Keccack.Current.CalculateHash(address.HexToByteArray());

        private static FollowerDb OpenFollowerAt(string path)
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = path, PathKeyedState = true });
            return new FollowerDb(manager, RocksDbChainStoreBundle.FromManager(manager, path, journalOptions: HistoricalStateOptions.FullArchive));
        }

        private static bool StoresStorageRoot(RocksDbManager manager, byte[] owner, byte[] storageRoot)
        {
            var root = manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, owner);
            return root != null && ByteUtil.AreEqual(Sha3Keccack.Current.CalculateHash(root), storageRoot);
        }

        private static int FlatSlotsOf(RocksDbManager manager, byte[] owner)
        {
            var slots = 0;
            using var it = manager.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            for (it.Seek(owner); it.Valid() && it.Key().Take(32).SequenceEqual(owner); it.Next()) slots++;
            return slots;
        }

        private async Task<(byte[] Owner, byte[] StorageRoot, int Slots)> StaticWhaleAsync(Live live)
        {
            var head = await live.Server.Bundle.Blocks.GetByNumberAsync(live.Head);
            var slots = (await live.Workload.Sequencer.State.GetAllStorageAsync(live.Workload.WhaleAddress)).Count;
            return (OwnerOf(live.Workload.WhaleAddress), ServerAccountAt(live, head.StateRoot, live.Workload.WhaleAddress).StateRoot, slots);
        }

        private async Task KillOnceTheWhaleTrieIsStoredAsync(Live live, (byte[] Owner, byte[] StorageRoot, int Slots) whale, string killedCopy)
        {
            live.NewLog();
            await using var db = live.Open();
            await db.Bundle.ResetSnapBootstrapStateAsync();
            using var cts = new CancellationTokenSource(RunTimeout);
            live.Scheduler.HoldAccountRangesExcept(whale.Owner);
            var run = live.RunAsync(db, cts.Token);
            Assert.True(await WaitUntilAsync(() => StoresStorageRoot(db.Manager, whale.Owner, whale.StorageRoot), StepTimeout),
                "phase 2 never committed the whale's storage trie");
            live.Scheduler.PauseStateRequests();
            db.Manager.CreateDatabaseCheckpoint(killedCopy);
            cts.Cancel();
            await StopAsync(run);
            live.Scheduler.ReleaseAccountRanges();
            live.Scheduler.ResumeStateRequests();
        }

        private async Task<int> ResumeUntilTheHealAsync(Live live, FollowerDb db, byte[] whaleOwner)
        {
            live.NewLog();
            using var cts = new CancellationTokenSource(RunTimeout);
            var phase2Ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            live.On(m =>
            {
                if (!StartsWith(m, HealEntry) && !StartsWith(m, "Snap-bootstrap: state populated")) return;
                cts.Cancel();
                phase2Ended.TrySetResult(true);
            });
            await StoppedAsync(live.RunAsync(db, cts.Token), phase2Ended.Task, "the resumed run never finished Phase 2");
            return FlatSlotsOf(db.Manager, whaleOwner);
        }

        [Fact]
        public Task Given_AWhaleCompletedAfterTheLastCheckpoint_When_TheNodeIsKilledAndPhase2Resumes_Then_EverySlotOfTheWhaleIsInFlatBeforeTheHeal()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static), staticTipAtHead: true);
                var whale = await StaticWhaleAsync(live);
                var killedCopy = live.DbPath + "-killed";
                try
                {
                    await KillOnceTheWhaleTrieIsStoredAsync(live, whale, killedCopy);
                    await using var resumed = OpenFollowerAt(killedCopy);
                    Assert.True(StoresStorageRoot(resumed.Manager, whale.Owner, whale.StorageRoot), "the kill lost the whale's committed storage trie");
                    Assert.True(FlatSlotsOf(resumed.Manager, whale.Owner) < whale.Slots,
                        "the whale's flat rows were durable at the kill; nothing after the last checkpoint was lost");

                    var flatSlots = await ResumeUntilTheHealAsync(live, resumed, whale.Owner);

                    Assert.Equal(whale.Slots, flatSlots);
                }
                finally
                {
                    try { if (Directory.Exists(killedCopy)) Directory.Delete(killedCopy, true); } catch { }
                }
            });

        [Fact]
        public Task Given_AWhaleCompletedBeforeTheLastCheckpoint_When_Phase2IsKilledAndResumed_Then_ItIsNotRefetched()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static), staticTipAtHead: true);
                var whale = await StaticWhaleAsync(live);
                live.NewLog();
                await using var db = live.Open();
                await db.Bundle.ResetSnapBootstrapStateAsync();
                using (var cts = new CancellationTokenSource(RunTimeout))
                {
                    var stopped = live.StopOnCrash(cts);
                    live.Scheduler.HoldAccountRangesExcept(whale.Owner);
                    var run = live.RunAsync(db, cts.Token);
                    Assert.True(await WaitUntilAsync(() => StoresStorageRoot(db.Manager, whale.Owner, whale.StorageRoot), StepTimeout),
                        "phase 2 never committed the whale's storage trie");
                    live.Scheduler.CrashStateRequests();
                    live.Scheduler.ReleaseAccountRanges();
                    await StoppedAsync(run, stopped, "the crash never stopped the first run");
                }
                Assert.Equal(whale.Slots, FlatSlotsOf(db.Manager, whale.Owner));
                live.Scheduler.Recover();
                live.Scheduler.CrashAfterStoragePagesOf(whale.Owner, int.MaxValue);

                var flatSlots = await ResumeUntilTheHealAsync(live, db, whale.Owner);

                Assert.Equal(0, live.Scheduler.PagesOfCrashOwner);
                Assert.Equal(whale.Slots, flatSlots);
            });

        private static int[] QueuedUnits(CapturingLogger log)
            => log.Messages.Select(m => Regex.Match(m, @"certify\.queue units=(\d+)"))
                .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToArray();

        private static int UnitsDoneAtGenerationStart(CapturingLogger log)
        {
            var start = log.Messages.First(m => m.Contains("snap.flat.certify start ", StringComparison.Ordinal));
            return int.Parse(Regex.Match(start, @"units_done=(\d+)/").Groups[1].Value);
        }

        private static byte[] FirstDepthTwoStorageKey(RocksDbManager manager, byte[] owner)
        {
            using var it = manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            for (it.Seek(owner); it.Valid() && it.Key().Take(32).SequenceEqual(owner); it.Next())
                if (it.Key().Length == 34) return it.Key();
            throw new Xunit.Sdk.XunitException("the whale has no stored depth-two storage node");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task Given_AStorageNodeDeletedAfterHealConverged_When_Finalized_Then_TheHoleIsHealedAndNoUnitIsHashedTwice(bool holed)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static), staticTipAtHead: true);
                var whale = OwnerOf(live.Workload.WhaleAddress);
                var log = live.NewLog();
                await using var db = live.Open();
                byte[] deleted = null;
                if (holed)
                    live.On(m =>
                    {
                        if (deleted != null || !StartsWith(m, "snap.flat.reconcile starting")) return;
                        deleted = FirstDepthTwoStorageKey(db.Manager, whale);
                        db.Manager.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, deleted);
                    });
                using var cts = new CancellationTokenSource(RunTimeout);

                var result = await CompletedAsync(live.RunAsync(db, cts.Token));

                await AssertSyncedStateAsync(live, db.Bundle, result, cts.Token, minPivot: 0);
                if (!holed)
                {
                    Assert.Equal(new[] { 256 }, QueuedUnits(log));
                    return;
                }
                Assert.NotNull(db.Manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, deleted));
                Assert.Equal(new[] { 256, 1 }, QueuedUnits(log));
            });

        [Fact]
        public Task Given_AHealThatEmptiedASlotAndAnAccount_When_Finalized_Then_TheFinalProveIsClean()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, revertingStorage: true));
                var log = live.NewLog();
                await using var db = live.Open();
                using var cts = new CancellationTokenSource(RunTimeout);
                var run = live.RunAsync(db, cts.Token);
                await ForceMovesAsync(live, log, 2);

                var result = await CompletedAsync(run);

                var pivot = await AssertSyncedStateAsync(live, db.Bundle, result, cts.Token, minPivot: live.StageTip(2) - SnapSyncOrchestrator.PivotServeTrailDistance);
                Assert.InRange((ulong)pivot.BlockNumber, RevertingStorageChangeBlock, RevertingStorageRevertBlock - 1);
                var reverting = live.Workload.RevertingStorageAddress;
                Assert.Equal(
                    ServerAccountAt(live, pivot.StateRoot, reverting).StateRoot.ToHex(),
                    StorageRootOf(await db.Bundle.State.GetAllStorageAsync(reverting)).ToHex());
            });

        [Fact]
        public Task Given_GenerationInterruptedAfterAPhase2WhoseRootMatched_When_TheNodeRestarts_Then_TheDoneUnitsAreKept()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false), staticTipAtHead: true);
                var log = live.NewLog();
                await using (var db = live.Open())
                {
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var repairs = 0;
                    live.On(m =>
                    {
                        if (StartsWith(m, "snap.flat.reconcile starting")) DeleteOneFlatAccountInEachOf(db.Manager, UnitsWithAFlatAccountDeleted);
                        if (m.Contains("snap.flat.certify.repair ", StringComparison.Ordinal) && Interlocked.Increment(ref repairs) == 64) cts.Cancel();
                    });
                    await StopAsync(live.RunAsync(db, cts.Token));
                    Assert.Equal(0, log.Count("from=Phase2 to=Phase3"));
                    Assert.InRange(UnitsDone(db.Manager), 64, 255);
                }
                var resumedLog = live.NewLog();
                await using var resumed = live.Open();
                using var resumedCts = new CancellationTokenSource(RunTimeout);

                var result = await CompletedAsync(live.RunAsync(resumed, resumedCts.Token));

                await AssertSyncedStateAsync(live, resumed.Bundle, result, resumedCts.Token, minPivot: 0);
                Assert.True(UnitsDoneAtGenerationStart(resumedLog) >= 64, "the restart discarded the done units of the interrupted generation");
            });

        [Fact]
        public Task Given_GenerationCompleteButNotCommitted_When_TheNodeRestarts_Then_OnlyTheDiffIsRegenerated()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false));
                var generatedCopy = live.DbPath + "-generated";
                try
                {
                    var firstLog = live.NewLog();
                    await using (var db = live.Open())
                    {
                        using var cts = new CancellationTokenSource(RunTimeout);
                        var generated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        live.On(m =>
                        {
                            if (generated.Task.IsCompleted || !StartsWith(m, "snap.flat.reconcile done")) return;
                            db.Manager.CreateDatabaseCheckpoint(generatedCopy);
                            cts.Cancel();
                            generated.TrySetResult(true);
                        });
                        var run = live.RunAsync(db, cts.Token);
                        await ForceMovesAsync(live, firstLog, 1);
                        Assert.True(await Task.WhenAny(generated.Task, Task.Delay(RunTimeout)) == generated.Task, "generation never completed");
                        await StopAsync(run);
                    }
                    live.JumpToHead();
                    var log = live.NewLog();
                    await using var resumed = OpenFollowerAt(generatedCopy);
                    Assert.Equal(0UL, resumed.Bundle.Metadata.GetLastBlock());
                    using var resumedCts = new CancellationTokenSource(RunTimeout);

                    var result = await CompletedAsync(live.RunAsync(resumed, resumedCts.Token));

                    await AssertSyncedStateAsync(live, resumed.Bundle, result, resumedCts.Token, minPivot: live.StageTip(0) + 1);
                    Assert.Equal(256, UnitsDoneAtGenerationStart(log));
                    Assert.Contains(log.Messages, m => m.Contains("snap.flat.certify.diff regions=", StringComparison.Ordinal));
                    Assert.Equal(new[] { 0 }, QueuedUnits(log));
                }
                finally
                {
                    try { if (Directory.Exists(generatedCopy)) Directory.Delete(generatedCopy, true); } catch { }
                }
            });

        private static readonly System.Numerics.BigInteger? MainnetShapedBlockGasLimit = new System.Numerics.BigInteger(8_000_000_000);
        private const int MainnetShapedLargeOwnerSlots = 70_000;
        private const int MainnetShapedCursoredOwnerSlots = 300_000;
        private const int EmbeddedLeafPairs = 64;
        private const int CursoredOwnerPagesBeforeTheKill = 1_500;
        private static readonly string[] NamedRepairCauses = { "first_pass", "diff", "phase2_large", "deferred_debt" };

        private sealed record MainnetShapedOwners(string LargeOwner, string CursoredOwner, string EmbeddedPairs);

        private static byte[] SlotLoopDeploy(int slots)
            => ("0x62" + slots.ToString("x6") + "60005b8181101561001a576001815560010161000656" + "5b00").HexToByteArray();

        private static byte[] SlotKey32(int index)
        {
            var key = new byte[32];
            key[29] = (byte)(index >> 16);
            key[30] = (byte)(index >> 8);
            key[31] = (byte)index;
            return key;
        }

        private static List<byte[]> EmbeddedLeafPairKeys()
        {
            const int candidates = 1 << 24;
            var packed = new ulong[candidates];
            Parallel.For(0, candidates, () => new byte[32], (i, _, hash) =>
            {
                Sha3Keccack.Current.CalculateHash(SlotKey32(i), 0, 32, hash, 0);
                var prefix = (ulong)hash[0] << 32 | (ulong)hash[1] << 24 | (ulong)hash[2] << 16 | (ulong)hash[3] << 8 | hash[4];
                packed[i] = prefix << 24 | (uint)i;
                return hash;
            }, _ => { });
            Array.Sort(packed);
            var keys = new List<byte[]>();
            for (int i = 1; i < candidates && keys.Count < 2 * EmbeddedLeafPairs; i++)
            {
                if (packed[i] >> 24 != packed[i - 1] >> 24) continue;
                keys.Add(SlotKey32((int)(packed[i - 1] & 0xFFFFFF)));
                keys.Add(SlotKey32((int)(packed[i] & 0xFFFFFF)));
                i++;
            }
            Assert.Equal(2 * EmbeddedLeafPairs, keys.Count);
            return keys;
        }

        private static async Task<MainnetShapedOwners> DeployMainnetShapedOwnersAsync(InProcessSequencerDriver sequencer)
        {
            var large = sequencer.QueueDeploy(
                sequencer.Accounts.All[TouchedAccounts + 4], SlotLoopDeploy(MainnetShapedLargeOwnerSlots), new System.Numerics.BigInteger(1_700_000_000));
            await ProduceSucceededAsync(sequencer, "70k-slot owner deploy");
            var cursored = sequencer.QueueDeploy(
                sequencer.Accounts.All[TouchedAccounts + 5], SlotLoopDeploy(MainnetShapedCursoredOwnerSlots), new System.Numerics.BigInteger(7_000_000_000));
            await ProduceSucceededAsync(sequencer, "300k-slot owner deploy");
            var setterOwner = sequencer.Accounts.All[TouchedAccounts + 6];
            var embedded = sequencer.QueueDeploy(setterOwner, ("0x610000" + LargeStorageLoopHex + SlotSetterRuntimeReturnHex).HexToByteArray(), PragueAirdropGas);
            await ProduceSucceededAsync(sequencer, "embedded-pair setter deploy");
            foreach (var key in EmbeddedLeafPairKeys())
                sequencer.QueueCall(setterOwner, embedded, key.Concat(new byte[31]).Append((byte)0x01).ToArray());
            await ProduceSucceededAsync(sequencer, "embedded-pair slots");
            Assert.Equal(MainnetShapedCursoredOwnerSlots, (await sequencer.State.GetAllStorageAsync(cursored)).Count);
            Assert.Equal(2 * EmbeddedLeafPairs, (await sequencer.State.GetAllStorageAsync(embedded)).Count);
            return new MainnetShapedOwners(large, cursored, embedded);
        }

        private const int UnitsWithAFlatAccountDeleted = 128;

        private static void DeleteOneFlatAccountInEachOf(RocksDbManager manager, int units)
        {
            var deleted = new HashSet<byte>();
            using var it = manager.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            for (it.SeekToFirst(); it.Valid() && deleted.Count < units; it.Next())
            {
                var key = it.Key();
                if (key.Length == 32 && deleted.Add(key[0])) manager.Delete(RocksDbManager.CF_STATE_ACCOUNTS, key);
            }
            Assert.Equal(units, deleted.Count);
        }

        private static int UnitsDone(RocksDbManager manager)
        {
            var done = 0;
            using var it = manager.CreateIterator(RocksDbManager.CF_FLAT_GENERATION);
            for (it.Seek(new byte[] { 0x02 }); it.Valid() && it.Key()[0] == 0x02; it.Next()) done++;
            return done;
        }

        private static byte[] StorageNodeUnderADoneUnit(RocksDbManager manager)
        {
            using var it = manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            for (it.SeekToFirst(); it.Valid(); it.Next())
            {
                var key = it.Key();
                if (key.Length == 34 && manager.Get(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02, key[0] }) != null) return key;
            }
            throw new Xunit.Sdk.XunitException("no stored depth-two storage node lies under a done unit");
        }

        private sealed class LeafWalkOnly : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public LeafWalkOnly(ITrieNodeStore inner) => _inner = inner;
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private sealed class E2eRun
        {
            private readonly Live _live;

            private string _path;

            public E2eRun(Live live)
            {
                _live = live;
                _path = live.DbPath;
                Db = live.Open();
            }

            public FollowerDb Db { get; private set; }
            public List<string> Copies { get; } = new();
            public CancellationTokenSource Cts { get; private set; }
            public Task<SnapBootstrapper.Result> Run { get; private set; }

            public CapturingLogger Start()
            {
                var log = _live.NewLog();
                Cts = new CancellationTokenSource(RunTimeout);
                Run = _live.RunAsync(Db, Cts.Token);
                return log;
            }

            public void Kill()
            {
                var copy = _live.DbPath + "-kill" + Copies.Count;
                Copies.Add(copy);
                Db.Manager.CreateDatabaseCheckpoint(copy);
                Cts.Cancel();
            }

            public async Task ReopenAtLastKillAsync()
            {
                await StopAsync(Run);
                await Db.DisposeAsync();
                _path = Copies[^1];
                Db = OpenFollowerAt(_path);
            }

            public async Task ReopenAfterCancelAsync()
            {
                await StopAsync(Run);
                await Db.DisposeAsync();
                Db = OpenFollowerAt(_path);
            }
        }

        private static async Task WhenAsync(Func<bool> condition, string failure, int pollMs = 5)
        {
            var deadline = DateTime.UtcNow + StepTimeout;
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, failure);
                await Task.Delay(pollMs);
            }
        }

        private static async Task KillPhase2AfterACursoredCheckpointAsync(Live live, E2eRun e2e, byte[] cursored)
        {
            live.Scheduler.CrashAfterStoragePagesOf(cursored, int.MaxValue);
            var log = e2e.Start();
            await ForceMovesAsync(live, log, 1);
            await WhenAsync(() => live.Scheduler.PagesOfCrashOwner >= CursoredOwnerPagesBeforeTheKill
                                  && e2e.Db.Manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, cursored) != null,
                "the 300k owner never flushed part of its storage trie", 1);
            live.Scheduler.PauseStateRequests();
            e2e.Kill();
            await e2e.ReopenAtLastKillAsync();
            live.Scheduler.Recover();
            live.Scheduler.ResumeStateRequests();
            Assert.True(FlatSlotsOf(e2e.Db.Manager, cursored) < MainnetShapedCursoredOwnerSlots, "the kill landed after the 300k owner was complete");
        }

        private static async Task AbandonPhase2ThenInterruptGenerationAsync(Live live, E2eRun e2e)
        {
            live.Scheduler.CrashStateRequests();
            var log = e2e.Start();
            var repairs = 0;
            live.On(m =>
            {
                if (StartsWith(m, "snap.bootstrap.retry")) live.Scheduler.Recover();
                if (StartsWith(m, "snap.bootstrap.resume phase=Phase2")) live.Scheduler.PauseStateRequests();
                if (StartsWith(m, "snap.flat.reconcile starting")) DeleteOneFlatAccountInEachOf(e2e.Db.Manager, UnitsWithAFlatAccountDeleted);
                if (m.Contains("snap.flat.certify.repair ", StringComparison.Ordinal) && Interlocked.Increment(ref repairs) == 64) e2e.Cts.Cancel();
            });
            await WhenAsync(() => live.Scheduler.Paused, "the abandoned Phase 2 never resumed", 5);
            await WhenAsync(() => log.Count("snap.pivot.phase2_start") >= 2, "the resumed Phase 2 never anchored its pivot", 5);
            var moves = PivotMoves(log.Messages);
            live.AdvanceStage();
            await WhenAsync(() => PivotMoves(log.Messages) > moves, "the resumed Phase 2 never adopted the moved pivot", 5);
            live.Scheduler.ResumeStateRequests();
            await StopAsync(e2e.Run);
            Assert.True(log.Count("snap.bootstrap.retry") > 0, "the injected Phase-2 exception never abandoned the run");
            await e2e.ReopenAfterCancelAsync();
            Assert.InRange(UnitsDone(e2e.Db.Manager), 64, 255);
            var hole = StorageNodeUnderADoneUnit(e2e.Db.Manager);
            var sink = ((IHealNodeSinkProvider)e2e.Db.Bundle).CreateHealSink();
            sink.DeleteNode(true, hole.Take(32).ToArray(), hole.Skip(32).ToArray());
            sink.Flush();
        }

        private static async Task KillTheHealForwardAfterItsFirstFlushAsync(Live live, E2eRun e2e)
        {
            var killed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestsBeforeTheRepairHeal = 0L;
            var armed = 0;
            var kills = 0;
            live.Scheduler.WithholdTrieNodes((_, _) =>
            {
                if (Volatile.Read(ref armed) == 0 || live.Scheduler.TrieNodeRequests < Interlocked.Read(ref requestsBeforeTheRepairHeal) + 2
                    || Interlocked.Increment(ref kills) != 1) return false;
                e2e.Kill();
                killed.TrySetResult(true);
                return false;
            });
            var log = e2e.Start();
            live.On(m =>
            {
                if (!StartsWith(m, "snap.flat.reheal attempt=1")) return;
                Interlocked.Exchange(ref requestsBeforeTheRepairHeal, live.Scheduler.TrieNodeRequests);
                Volatile.Write(ref armed, 1);
            });
            Assert.True(await Task.WhenAny(killed.Task, Task.Delay(RunTimeout)) == killed.Task, "the repair heal-forward never reached its second round");
            Assert.True(UnitsDoneAtGenerationStart(log) >= 64, "the restart lost the journal of the interrupted generation");
            live.Scheduler.ServeAllTrieNodes();
            await e2e.ReopenAtLastKillAsync();
        }

        private static async Task KillTheDiffAfterItsFirstAccountEmitAsync(Live live, E2eRun e2e)
        {
            live.AdvanceStage();
            var killed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var emits = 0;
            var log = e2e.Start();
            live.On(m =>
            {
                if (!m.Contains("snap.flat.certify.repair ", StringComparison.Ordinal) || !m.Contains(" owner=0x range=", StringComparison.Ordinal)
                    || !m.Contains(" cause=diff", StringComparison.Ordinal) || Interlocked.Increment(ref emits) != 1) return;
                e2e.Kill();
                killed.TrySetResult(true);
            });
            Assert.True(await Task.WhenAny(killed.Task, Task.Delay(RunTimeout)) == killed.Task, "the diff never emitted an account range");
            Assert.True(UnitsDoneAtGenerationStart(log) > 0, "the restart lost the journal before the diff");
            await e2e.ReopenAtLastKillAsync();
        }

        private static void AssertEveryAddedOrGhostRepairIsAttributed(IEnumerable<string> messages)
        {
            foreach (var line in messages.Where(m => m.Contains("snap.flat.certify.repair ", StringComparison.Ordinal)))
            {
                var counts = Regex.Match(line, @" added=(\d+)/(\d+) ghost=(\d+)/(\d+) ");
                Assert.True(counts.Success, line);
                if (Enumerable.Range(1, 4).All(g => counts.Groups[g].Value == "0")) continue;
                Assert.Contains(Regex.Match(line, @" cause=(\w+)").Groups[1].Value, NamedRepairCauses);
            }
        }

        private static async Task AssertZeroRepairsAsync(RocksDbChainStoreBundle bundle, byte[] root)
        {
            var verify = await ((IFlatStateReconciler)bundle).VerifyFlatStateAsync(root, null, CancellationToken.None);
            Assert.Equal(0, verify.TotalRepairs);
            Assert.Equal(0, verify.TrieNodesMissing);
            Assert.Equal(0, verify.TrieNodesDiffering);
            var oracle = await new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new LeafWalkOnly(bundle.StateTrieNodes))
                .VerifyFlatStateAsync(root, null, CancellationToken.None);
            Assert.Equal(0, oracle.TotalRepairs);
        }

        [Fact]
        public Task Given_AMainnetShapedWorkloadWithRevertingStorage_When_Phase2IsKilledAndAbandonedStateChangesUnderHealGenerationIsInterruptedAndAHealForwardIsKilled_Then_TheFinalProveFindsZeroRepairs()
            => Observed(async () =>
            {
                var budget = System.Diagnostics.Stopwatch.StartNew();
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, revertingStorage: true, mainnetShaped: true));
                var e2e = new E2eRun(live);
                try
                {
                    await KillPhase2AfterACursoredCheckpointAsync(live, e2e, OwnerOf(live.Workload.MainnetShaped.CursoredOwner));
                    await AbandonPhase2ThenInterruptGenerationAsync(live, e2e);
                    await KillTheHealForwardAfterItsFirstFlushAsync(live, e2e);
                    await KillTheDiffAfterItsFirstAccountEmitAsync(live, e2e);
                    var log = e2e.Start();

                    var result = await CompletedAsync(e2e.Run);

                    var reconciled = log.Messages.Last(m => m.Contains("snap.flat.reconcile starting root=0x", StringComparison.Ordinal));
                    Assert.Contains("root=0x" + result.PivotStateRoot.ToHex() + " ", reconciled);
                    await AssertSyncedStateAsync(live, e2e.Db.Bundle, result, CancellationToken.None, minPivot: 0);
                    await AssertZeroRepairsAsync(e2e.Db.Bundle, result.PivotStateRoot);
                    AssertEveryAddedOrGhostRepairIsAttributed(live.AllMessages);
                    Assert.True(budget.Elapsed < TimeSpan.FromMinutes(7), $"the e2e took {budget.Elapsed}, over its 7-minute budget");
                }
                finally
                {
                    await e2e.Db.DisposeAsync();
                    foreach (var copy in e2e.Copies)
                        try { if (Directory.Exists(copy)) Directory.Delete(copy, true); } catch { }
                }
            });
    }
}
