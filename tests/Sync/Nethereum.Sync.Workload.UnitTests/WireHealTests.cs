using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.Contracts;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Collection(Snap2LiveHarness.Collection)]
    public partial class WireHealTests
    {
        private const int Accounts = 2200;
        private const int Blocks = 170;
        private const ulong InitialPivot = 70;
        private const int TouchedAccounts = 200;

        private const int ActorAccounts = 300;
        private const int BulkEoas = 5000;
        private const int ChurnBlocks = 220;
        private const int BulkTransfersPerBlock = 6;
        private const ulong StageStep = 40;
        private const int Stages = 4;
        private const ulong StalePivotDistance = 20;
        private const int PagesBeforeEachMove = 4;
        private const int PagesBeforeCrash = 6;
        private const int SnapResponseLimit = 4 * 1024;
        private const int WhaleAirdropBatches = 12;
        private const int WhaleHoldersPerBatch = 100;
        private const int WhaleHolders = WhaleAirdropBatches * WhaleHoldersPerBatch;
        private const int ChurnWindow = 600;
        private const int WhaleCrashAfterPages = 3;
        private const string LargeStorageLoopHex = "60005b81811015610019576001815560010161000556" + "5b";
        private const string SelfDestructToOneHex = "730000000000000000000000000000000000000001ff";
        private static readonly BigInteger PragueAirdropGas = new BigInteger(5_000_000);
        private static readonly BigInteger AmsterdamAirdropGas = new BigInteger(30_000_000);
        private static readonly BigInteger PragueLargeStorageDeployGas = new BigInteger(29_000_000);
        private static readonly BigInteger AmsterdamLargeStorageDeployGas = new BigInteger(40_000_000);
        private const int PragueSelfDestructedSlots = 1200;
        private const int AmsterdamSelfDestructedSlots = 150;
        private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(300);
        private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan ServerIdleTimeout = TimeSpan.FromMinutes(30);

        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);
        private readonly ITestOutputHelper _out;
        public WireHealTests(ITestOutputHelper @out) => _out = @out;

        private sealed class GatedTipSource : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _initial;
            private readonly CanonicalTip _head;
            private readonly Func<bool> _advanced;
            public GatedTipSource(CanonicalTip initial, CanonicalTip head, Func<bool> advanced)
            {
                _initial = initial;
                _head = head;
                _advanced = advanced;
            }
            public string Name => "GatedTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(_advanced() ? _head : _initial);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong n, CancellationToken ct)
                => Task.FromResult(((byte[])null, (byte[])null));
        }

        private sealed class StagedTipSource : ICanonicalStateRootSource
        {
            private readonly CanonicalTip[] _stages;
            private readonly Func<int> _stageIndex;
            public StagedTipSource(CanonicalTip[] stages, Func<int> stageIndex)
            {
                _stages = stages;
                _stageIndex = stageIndex;
            }
            public string Name => "StagedTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(_stages[Math.Min(_stageIndex(), _stages.Length - 1)]);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong n, CancellationToken ct)
                => Task.FromResult(((byte[])null, (byte[])null));
        }

        private static async Task<CanonicalTip> TipAtAsync(WireServerNode server, ulong block)
        {
            var header = await server.Bundle.Blocks.GetByNumberAsync(block);
            var hash = await server.Bundle.Blocks.GetHashByNumberAsync(block);
            return new CanonicalTip { BlockNumber = block, BlockHash = hash, StateRoot = header.StateRoot };
        }

        [Fact]
        public async Task Follower_RollingPivot_AcrossChangedState_TriggersHeal_AndRecovers()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: Accounts);
            for (var i = 0; i < Blocks; i++)
            {
                sequencer.QueueTransfer(sequencer.Accounts.All[i % TouchedAccounts], sequencer.Accounts.All[(i + 1) % TouchedAccounts].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            await using var server = await WireServerNode.StartAsync(sequencer, snapResponseLimit: 4 * 1024);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var finalHeader = await server.Bundle.Blocks.GetByNumberAsync(head);

            var tipAdvanced = 0;
            var source = new GatedTipSource(
                await TipAtAsync(server, InitialPivot),
                await TipAtAsync(server, head),
                () => Volatile.Read(ref tipAdvanced) == 1);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)), "no snap peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-heal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var log = new CapturingLogger();

                var runTask = SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, source, activations, log,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true, RootRefreshIntervalMs = 50 }, cts.Token);

                Assert.True(await WaitUntilAsync(
                        () => log.Messages.Any(m => m.Contains("snap.verify", StringComparison.OrdinalIgnoreCase)),
                        TimeSpan.FromSeconds(60)),
                    "phase 2 never started streaming");
                Volatile.Write(ref tipAdvanced, 1);

                var result = await runTask;
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                Assert.Contains(log.Messages, m => m.Contains("entering heal phase", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(log.Messages, m => m.Contains("Heal complete", StringComparison.OrdinalIgnoreCase) && m.Contains("matched=True"));

                var pivot = result.PivotBlockNumber;
                Assert.True(pivot > InitialPivot, $"pivot never rotated (still {pivot})");
                var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(pivot);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                foreach (var account in sequencer.Accounts.All.Take(TouchedAccounts))
                {
                    var synced = await FollowerStateAtPivot.ReadAsync(bundle, pivotHeader.StateRoot, account.Address);
                    Assert.NotNull(synced);
                    var expectedLeaf = serverAtPivot.Get(Sha3Keccack.Current.CalculateHash(account.Address.HexToByteArray()));
                    Assert.NotNull(expectedLeaf);
                    Assert.Equal(accountDecoder.Decode(expectedLeaf).Balance, synced.Balance);
                }

                Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
                Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
                var reconciler = Assert.IsAssignableFrom<IFlatStateReconciler>(bundle);
                Assert.Empty(reconciler.GetPersistedDamage());
                var verify = await reconciler.VerifyFlatStateAsync(pivotHeader.StateRoot, null, cts.Token);
                Assert.Equal(0, verify.TotalRepairs);
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_PathKeyedRocksDbFollower_When_ProducerMutatesAccountsAndDeletesStorageSlotsWhileSyncing_Then_SnapCompletesWithFlatEqualToTrie(bool snap2)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2));
                var log = live.NewLog();
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 2);
                    var result = await CompletedAsync(run);

                    AssertModeSpecificMechanism(live, result, minPivotMoves: 2);
                    await AssertSyncedStateAsync(live, bundle, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);
                    if (!snap2)
                        Assert.Equal(0, ReconcileSlotsAdded(log));
                }
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_PathKeyedRocksDbFollower_When_PivotMovesWhileNodeIsDown_Then_ResumedSyncCatchesUpWithFlatEqualToTrie(bool snap2)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2));
                var log1 = live.NewLog();
                ulong savedPivot;
                await using (var bundle1Db = live.Open())
                {
                    var (_, bundle1) = bundle1Db;
                    await bundle1.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var stopped = live.StopOnCrash(cts);
                    live.On(m => { if (StartsWith(m, "snap.verify") && Interlocked.Increment(ref live.VerifiedPages) == PagesBeforeCrash) live.Scheduler.CrashStateRequests(); });
                    await StoppedAsync(live.RunAsync(bundle1Db, cts.Token), stopped, "the crash never stopped the first run");
                    var saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
                    Assert.True(saved.Counters.AccountsSynced > 0, "the crash persisted no Phase-2 progress; the resume would be a fresh sync");
                    savedPivot = saved.PivotBlockNumber;
                }
                Assert.DoesNotContain(log1.Messages, m => m.Contains("Snap-bootstrap: state populated", StringComparison.Ordinal));

                live.Scheduler.Recover();
                live.AdvanceStage();
                var log2 = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    var (_, bundle2) = bundle2Db;
                    using var cts = new CancellationTokenSource(RunTimeout);
                    live.Scheduler.PauseStateRequests();
                    var run = live.RunAsync(bundle2Db, cts.Token);
                    Assert.True(
                        await WaitUntilAsync(() => AdoptedAMovedTip(snap2, log2, savedPivot), StepTimeout),
                        "the resumed run never adopted the tip that moved while the node was down");
                    live.Scheduler.ResumeStateRequests();
                    var result = await CompletedAsync(run);

                    Assert.True(result.PivotBlockNumber > savedPivot, $"the resumed pivot {result.PivotBlockNumber} did not move past the saved {savedPivot}");
                    Assert.True(
                        log2.Count(snap2 ? "snap.bootstrap.snap2_resume phase=Phase2Running" : "snap.bootstrap.pivot_moved") > 0
                        || log2.Count("snap.bootstrap.resume phase=Phase2") > 0,
                        "the restart did not resume the saved download");
                    AssertModeSpecificMechanism(live, result, minPivotMoves: 0);
                    if (snap2)
                        Assert.True(live.Metrics.BalHealBlocksAppliedTotal > 0, "snap/2 closed the gap without applying a single BAL block");
                    await AssertSyncedStateAsync(live, bundle2, result, cts.Token, minPivot: savedPivot + 1);
                }
            });

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public Task Given_HashOrPathKeyedRocksDbFollower_When_NodeRestartsMidDownload_Then_ResumedSyncCompletesWithFlatEqualToTrie(bool snap2, bool pathKeyed)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2), pathKeyed: pathKeyed);
                var log1 = live.NewLog();
                await using (var bundle1Db = live.Open())
                {
                    var (_, bundle1) = bundle1Db;
                    await bundle1.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundle1Db, cts.Token);
                    await ForceMovesAsync(live, log1, 1);
                    cts.Cancel();
                    var stopped = await StopAsync(run);
                    Assert.False(stopped?.Ran ?? false, "the first run finished before it could be stopped");
                    var saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
                    _out.WriteLine($"graceful stop persisted phase={saved.Phase} pivot={saved.PivotBlockNumber} tasks={saved.Tasks.Count} accounts_synced={saved.Counters.AccountsSynced}");
                }
                Assert.DoesNotContain(log1.Messages, m => m.Contains("Snap-bootstrap: state populated", StringComparison.Ordinal));

                var log2 = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    var (_, bundle2) = bundle2Db;
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundle2Db, cts.Token);
                    await ForceMovesAsync(live, log2, 1);
                    var result = await CompletedAsync(run);

                    Assert.True(
                        log2.Count("snap.bootstrap.snap2_resume phase=Phase2Running") > 0
                        || log2.Count("snap.bootstrap.pivot_moved") > 0
                        || log2.Count("snap.bootstrap.resume phase=Phase2") > 0,
                        "the restart did not route through the resume path");
                    AssertModeSpecificMechanism(live, result, minPivotMoves: 2);
                    await AssertSyncedStateAsync(live, bundle2, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);
                }
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_CleanSync_When_Snap2_Then_FollowerSendsZeroGetTrieNodesRequests(bool snap2)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2));
                var log = live.NewLog();
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 2);
                    var result = await CompletedAsync(run);

                    AssertModeSpecificMechanism(live, result, minPivotMoves: 2);
                    await AssertSyncedStateAsync(live, bundle, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);
                    _out.WriteLine($"GetTrieNodes requests={live.Scheduler.TrieNodeRequests}");
                    if (snap2)
                        Assert.Equal(0, live.Scheduler.TrieNodeRequests);
                    else
                        Assert.True(live.Scheduler.TrieNodeRequests > 0, "snap/1 heal is expected to fetch trie nodes over the wire");
                }
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_SnapCompletes_When_SequencerProducesMoreBlocks_Then_Phase4ExecutesThemToMatchingRootUsingTheProductionExecutor(bool snap2)
            => Observed(async () =>
            {
                var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: Hardfork(snap2));
                const int InitialChainBlocks = 90;
                for (var i = 0; i < InitialChainBlocks; i++)
                {
                    sequencer.QueueTransfer(sequencer.Accounts.All[i % 10], sequencer.Accounts.All[(i + 1) % 10].Address, Eth(1));
                    await sequencer.ProduceBlockAsync();
                }

                await using var live = await StartLiveAsync(snap2, new Workload(sequencer, null, null), staticTipAtHead: true, snapResponseLimit: 0);
                var log = live.NewLog();
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var result = await CompletedAsync(live.RunAsync(bundleDb, cts.Token));
                    var pivotHeader = await live.Server.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                    Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());

                    var reconciler = Assert.IsAssignableFrom<IFlatStateReconciler>(bundle);
                    Assert.Empty(reconciler.GetPersistedDamage());
                    var certified = await reconciler.VerifyFlatStateAsync(pivotHeader.StateRoot, null, cts.Token);
                    Assert.Equal(0, certified.TotalRepairs);

                    const int NewBlocksAfterSync = 6;
                    for (var i = 0; i < NewBlocksAfterSync; i++)
                    {
                        sequencer.QueueTransfer(sequencer.Accounts.All[i % 10], sequencer.Accounts.All[(i + 2) % 10].Address, Eth(1));
                        await sequencer.ProduceBlockAsync();
                    }
                    Assert.True(await live.Server.ImportPendingProducedBlocksAsync() >= NewBlocksAfterSync);
                    var newHead = (ulong)await sequencer.Blocks.GetHeightAsync();

                    var chainConfig = new Nethereum.CoreChain.ChainConfig { ChainId = sequencer.ChainId, BaseFee = BigInteger.Zero, Coinbase = sequencer.SequencerAddress, Hardfork = sequencer.Hardfork };
                    var hardforkConfig = chainConfig.GetHardforkConfig();
                    Nethereum.CoreChain.Sync.IBlockExecutor executor = Nethereum.CoreChain.FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                        bundle, new FixedChainActivations(HardforkNames.Parse(sequencer.Hardfork)),
                        _ => chainConfig, _ => hardforkConfig, Nethereum.CoreChain.NoRewardPolicy.Instance);
                    var followed = 0;
                    foreach (var (header, txs) in sequencer.ProducedBlockData.Where(b => (ulong)b.Header.BlockNumber > result.PivotBlockNumber))
                    {
                        var blockWithdrawals = await sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber);
                        var r = await executor.ProcessBlockAsync(
                            header, txs, null,
                            blockWithdrawals, CancellationToken.None);
                        Assert.True(r.RootMatches, $"Phase 4 follower diverged executing block {header.BlockNumber}");
                        bundle.Metadata.Commit((ulong)header.BlockNumber, r.BlockHash);
                        followed++;
                    }
                    Assert.True(followed >= NewBlocksAfterSync,
                        $"expected Phase 4 to execute at least {NewBlocksAfterSync} post-sync blocks; executed {followed}");

                    Assert.Equal(
                        await live.Server.Bundle.Blocks.GetHashByNumberAsync(newHead),
                        await bundle.Blocks.GetHashByNumberAsync(newHead));
                    var serverHeader = await live.Server.Bundle.Blocks.GetByNumberAsync(newHead);
                    var followerHeader = await bundle.Blocks.GetByNumberAsync(newHead);
                    Assert.Equal(serverHeader.StateRoot.ToHex(), followerHeader.StateRoot.ToHex());
                }
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_PivotMovesAgainAfterPhase2Ends_When_Snap1HealsOrSnap2Freezes_Then_FinalPivotIsCorrect(bool snap2)
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2));
                var log = live.NewLog();
                var phaseEnd = snap2 ? "snap.generate.start" : "snap.phase.transition from=Phase2 to=Phase3";
                string phaseEndMessage = null;
                live.On(m =>
                {
                    if (!StartsWith(m, phaseEnd) || Interlocked.CompareExchange(ref phaseEndMessage, m, null) != null) return;
                    live.JumpToHead();
                });
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 2);
                    var result = await CompletedAsync(run);

                    Assert.True(phaseEndMessage != null, $"'{phaseEnd}' was never logged, so the tip never moved after Phase 2 ended");
                    var pivotAtPhaseEnd = ulong.Parse(Regex.Match(phaseEndMessage, @"pivot=(\d+)").Groups[1].Value);
                    AssertModeSpecificMechanism(live, result, minPivotMoves: 2);
                    if (snap2)
                    {
                        Assert.True(live.Head - SnapSyncOrchestrator.PivotServeTrailDistance - pivotAtPhaseEnd > StalePivotDistance,
                            $"the late tip {live.Head} is not stale against the frozen pivot {pivotAtPhaseEnd}; the freeze check would be vacuous");
                        Assert.Equal(pivotAtPhaseEnd, result.PivotBlockNumber);
                    }
                    else
                    {
                        Assert.True(result.PivotBlockNumber >= pivotAtPhaseEnd,
                            $"the healed pivot {result.PivotBlockNumber} went backwards from the Phase-2 pivot {pivotAtPhaseEnd}");
                    }
                    await AssertSyncedStateAsync(live, bundle, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);
                }
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_WhaleContractEmptiedBeforePivot_When_Synced_Then_FlatEqualsTrieWithNoLeftoverStorage(bool snap2)
            => RunWhaleLifecycleScenarioAsync(snap2, WhaleShape.Emptied, selfDestructLargeContract: false);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_LargeStorageContractSelfDestructedBeforePivot_When_Synced_Then_FlatEqualsTrieWithNoLeftoverStorage(bool snap2)
            => RunWhaleLifecycleScenarioAsync(snap2, WhaleShape.Churned, selfDestructLargeContract: true);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Given_NodeRestartsWhileWhaleStorageSubtaskInFlight_When_Resumed_Then_WhaleStorageMatchesServerAtFinalPivotInFlat(bool snap2)
            => Observed(async () =>
            {
                var workload = await BuildWorkloadAsync(snap2, WhaleShape.Static);
                await using var live = await StartLiveAsync(snap2, workload, staticTipAtHead: true, largeContractConcurrency: 1);
                var whaleHash = Sha3Keccack.Current.CalculateHash(workload.WhaleAddress.HexToByteArray());
                var serverWhale = await workload.Sequencer.State.GetAllStorageAsync(workload.WhaleAddress);
                Assert.True(serverWhale.Count >= WhaleHolders, $"the whale holds only {serverWhale.Count} slots");

                var log1 = live.NewLog();
                SnapSyncState saved;
                await using (var bundle1Db = live.Open())
                {
                    var (_, bundle1) = bundle1Db;
                    await bundle1.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var stopped = live.StopOnCrash(cts);
                    live.Scheduler.CrashAfterStoragePagesOf(whaleHash, WhaleCrashAfterPages);
                    await StoppedAsync(live.RunAsync(bundle1Db, cts.Token), stopped, "the node never crashed with the whale in flight");
                    Assert.True(live.Scheduler.PagesOfCrashOwner >= WhaleCrashAfterPages);

                    saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
                    var whaleSubtasks = saved.Tasks
                        .SelectMany(t => t.SubTasks)
                        .Where(kv => ByteUtil.AreEqual(kv.Key, whaleHash))
                        .SelectMany(kv => kv.Value)
                        .ToList();
                    Assert.True(whaleSubtasks.Count > 0, "the whale had no cursored storage subtask at the stop; the batch never truncated at it");
                    Assert.Contains(whaleSubtasks, s => s.Next.Any(b => b != 0));
                    var flatAtStop = await bundle1.State.GetAllStorageAsync(workload.WhaleAddress);
                    _out.WriteLine($"stop: whale subtasks={whaleSubtasks.Count} cursors={string.Join(",", whaleSubtasks.Select(s => s.Next.ToHex().Substring(0, 8)))} flat_slots={flatAtStop.Count}/{serverWhale.Count}");
                    Assert.InRange(flatAtStop.Count, 1, serverWhale.Count - 1);
                }

                live.Scheduler.Recover();
                var log2 = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    var (_, bundle2) = bundle2Db;
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var result = await CompletedAsync(live.RunAsync(bundle2Db, cts.Token));

                    Assert.True(
                        log2.Count(snap2 ? "snap.bootstrap.snap2_resume phase=Phase2Running" : "snap.bootstrap.resume phase=Phase2") > 0,
                        "the restart did not resume the saved whale cursor");
                    Assert.Equal(saved.PivotBlockNumber, result.PivotBlockNumber);
                    Assert.Null(bundle2.Metadata.GetDeferredHealAccountsBlob());
                    Assert.DoesNotContain(whaleHash.ToHex(), live.Scheduler.TrieNodeStorageOwners);
                    Assert.Equal(0UL, bundle2.Metadata.CountOpenDeferredStorageDebts());
                    _out.WriteLine($"resume: trie-node requests={live.Scheduler.TrieNodeRequests} storage owners healed={live.Scheduler.TrieNodeStorageOwners.Count} heal_entered={live.AllMessages.Any(m => m.Contains("entering heal phase", StringComparison.Ordinal))}");
                    if (snap2)
                    {
                        Assert.Equal(0, live.AllMessages.Count(m => m.Contains("entering heal phase", StringComparison.Ordinal)));
                        Assert.Equal(0L, live.Metrics.Phase3NodesHealedTotal);
                        Assert.Equal(0, live.Scheduler.TrieNodeRequests);
                        Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(log2));
                    }

                    var pivotHeader = await AssertSyncedStateAsync(live, bundle2, result, cts.Token, minPivot: 0);
                    var expectedStorageRoot = ServerAccountAt(live, pivotHeader.StateRoot, workload.WhaleAddress).StateRoot;
                    var flat = await bundle2.State.GetAllStorageAsync(workload.WhaleAddress);
                    Assert.Equal(serverWhale.Count, flat.Count);
                    Assert.Equal(expectedStorageRoot.ToHex(), StorageRootOf(flat).ToHex());
                    var rawReader = Assert.IsAssignableFrom<IRawNodeReader>(bundle2.StateTrieNodes);
                    var whaleTrie = PatriciaTrie.ReattachFromRawRoot(rawReader, bundle2.StateTrieNodes, whaleHash);
                    Assert.NotNull(whaleTrie);
                    Assert.Equal(expectedStorageRoot.ToHex(), whaleTrie.Root.GetHash().ToHex());
                }
            });

        [Fact]
        public Task Given_ReplacedSubtreeWithMissingOldNodeLocally_When_Healed_Then_HealCompletesWithoutThrowing()
            => Observed(async () =>
            {
                const bool snap2 = false;
                await using var live = await StartLiveAsync(snap2, await BuildWorkloadAsync(snap2));
                var log1 = live.NewLog();
                SnapSyncState saved;
                await using (var bundle1Db = live.Open())
                {
                    var (_, bundle1) = bundle1Db;
                    await bundle1.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    live.Scheduler.HoldTrieNodeRequests();
                    var run = live.RunAsync(bundle1Db, cts.Token);
                    await ForceMovesAsync(live, log1, 2);
                    Assert.True(
                        await WaitUntilAsync(() => log1.Count("from=Phase2 to=Phase3") > 0 && live.Scheduler.TrieNodeRequests > 0, StepTimeout),
                        "phase 2 never handed off to a trie-node heal");
                    cts.Cancel();
                    var stopped = await StopAsync(run);
                    Assert.False(stopped?.Ran ?? false, "the heal finished before it could be stopped");
                    saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase3Running, saved.Phase);
                }

                var evicted = await EvictAStaleDepthOneAccountNodeAsync(live, saved.HealTargetRoot);
                _out.WriteLine($"evicted stale account node at nibble {evicted:x} under heal target 0x{saved.HealTargetRoot.ToHex()}");

                live.Scheduler.ReleaseTrieNodeRequests();
                var log2 = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    var (manager2, bundle2) = bundle2Db;
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var result = await CompletedAsync(live.RunAsync(bundle2Db, cts.Token));

                    Assert.True(log2.Count("snap.bootstrap.resume phase=Phase3") > 0, "the restart did not resume the saved heal");
                    Assert.Equal(0, log2.Count("snap.bootstrap.retry"));
                    Assert.Contains(log2.Messages, m => m.Contains("Heal complete", StringComparison.Ordinal) && m.Contains("matched=True"));
                    Assert.NotNull(manager2.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, new[] { (byte)evicted }));
                    await AssertSyncedStateAsync(live, bundle2, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);
                }
            });

        [Fact]
        [Trait("Reproducer", "mainnet-snap1-heal-unresolvable-storage")]
        public Task Given_Snap1HealRotatesAcrossAStorageTrieThatIsRewrittenThenReverted_When_ARotationLandsAfterADescendantPathWasOverwrittenButBeforeItsAncestor_Then_EveryStorageTrieAtTheHealedPivotIsCompleteAndReconcileFindsNoUnresolvableSubtree()
            => RunRewriteThenRevertRotationAsync(
                rotation =>
                {
                    rotation.Live.On(m =>
                    {
                        if (!StartsWith(m, "Heal complete") || !m.Contains("matched=True", StringComparison.Ordinal) || rotation.UnresolvableAtHeal != null) return;
                        rotation.UnresolvableAtHeal = StorageTriesUnresolvableAt(rotation.Bundle, RootAfter(m, "target=0x"));
                        rotation.PathsAtHeal = DescribeStoragePaths(rotation.Live, rotation.Manager, rotation.Owner, rotation.RevertedStorageRoot, rotation.RewrittenStorageRoot);
                    });
                },
                rotation =>
                {
                    var reconcileDamage = rotation.Log.Messages.Where(m => m.Contains("unresolvable storage subtree", StringComparison.Ordinal)).ToList();
                    _out.WriteLine($"local storage paths at heal completion: {rotation.PathsAtHeal}");
                    foreach (var u in rotation.UnresolvableAtHeal ?? new List<string>()) _out.WriteLine($"unresolvable at heal completion: {u}");
                    foreach (var d in reconcileDamage) _out.WriteLine($"reconcile: {d}");

                    Assert.Equal(rotation.RevertedRoot.ToHex(), rotation.HealedTarget);
                    Assert.True(rotation.UnresolvableAtHeal.Count == 0,
                        $"heal reported matched=True at 0x{rotation.HealedTarget} but {rotation.UnresolvableAtHeal.Count} storage trie(s) reachable from it are not walkable: {string.Join(" | ", rotation.UnresolvableAtHeal)}; local paths: {rotation.PathsAtHeal}");
                    Assert.True(reconcileDamage.Count == 0, $"flat reconcile found unresolvable storage subtrees after a converged heal: {string.Join(" | ", reconcileDamage)}");
                    Assert.True(rotation.RunFailure == null, rotation.RunFailure?.ToString());
                    Assert.True(rotation.Result.Ran, rotation.Result.SkipReason);
                });

        [Fact]
        [Trait("Reproducer", "mainnet-snap1-heal-unresolvable-storage")]
        public Task Given_TheRewriteThenRevertRotation_When_TheHealRotatesAwayFromTheRewrittenPivot_Then_TheContractsStorageRootAccountLeafAndWithheldRewrittenChildAreAbsentAndTheServedOneHoldsItsRewrittenVersion()
            => RunRewriteThenRevertRotationAsync(
                rotation =>
                {
                    var leafPath = AccountLeafPath(rotation.Live, rotation.RewrittenRoot, rotation.Owner);
                    rotation.Live.On(m =>
                    {
                        if (!MovesHealTo(m, rotation.RevertedRoot) || rotation.OnDiskAtRetarget != null) return;
                        rotation.OnDiskAtRetarget = new Dictionary<string, byte[]>
                        {
                            ["storage root owner||[]"] = rotation.Manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, rotation.Owner),
                            ["account leaf"] = rotation.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, leafPath),
                            [$"withheld rewritten child owner||[{rotation.Slots.WithheldNibble:x}]"] = rotation.Manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, rotation.Owner.Concat(new[] { (byte)rotation.Slots.WithheldNibble }).ToArray()),
                        };
                        rotation.ServedChildAtRetarget = rotation.Manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, rotation.Owner.Concat(new[] { (byte)rotation.Slots.RewrittenNibble }).ToArray());
                    });
                },
                rotation =>
                {
                    Assert.True(rotation.OnDiskAtRetarget != null, "the heal never signalled the retarget to the reverted pivot");
                    foreach (var kv in rotation.OnDiskAtRetarget) _out.WriteLine($"at retarget to the reverted pivot: {kv.Key} = {HashOf(kv.Value)}");
                    Assert.All(rotation.OnDiskAtRetarget, kv => Assert.True(kv.Value == null,
                        $"{kv.Key} still holds 0x{HashOf(kv.Value)} on disk when the heal rotated away from the rewritten pivot"));
                    var rewrittenChild = (ServerBranch(rotation.Live, rotation.RewrittenStorageRoot).Children[rotation.Slots.RewrittenNibble] as HashNode)?.Hash;
                    Assert.Equal(rewrittenChild?.ToHex(), HashOf(rotation.ServedChildAtRetarget));
                });

        [Fact]
        [Trait("Reproducer", "mainnet-snap1-heal-unresolvable-storage")]
        public Task Given_TheRewriteThenRevertRotation_When_HealingTheRevertedPivot_Then_TheWithheldChildIsFetchedAtMostOnceAndNothingBelowItIsRequested()
        {
            var withheldChildRequests = 0;
            var requestsBelowWithheldChild = 0;
            return RunRewriteThenRevertRotationAsync(
                rotation =>
                {
                    rotation.OnPathset = (root, pathset) =>
                    {
                        if (rotation.HealedTarget != null || !ByteUtil.AreEqual(root, rotation.RevertedRoot) || pathset.Count < 2 || !ByteUtil.AreEqual(pathset[0], rotation.Owner)) return;
                        foreach (var compact in pathset.Skip(1))
                        {
                            var nibbles = PatriciaPathWalker.CompactToNibbles(compact);
                            if (nibbles.Length == 0 || nibbles[0] != rotation.Slots.WithheldNibble) continue;
                            if (nibbles.Length == 1) Interlocked.Increment(ref withheldChildRequests);
                            else Interlocked.Increment(ref requestsBelowWithheldChild);
                        }
                    };
                },
                rotation =>
                {
                    _out.WriteLine($"at the reverted pivot: owner||[{rotation.Slots.WithheldNibble:x}] requested {withheldChildRequests}x, below it {requestsBelowWithheldChild}x");
                    Assert.Equal(rotation.RevertedRoot.ToHex(), rotation.HealedTarget);
                    Assert.True(withheldChildRequests <= 1, $"owner||[{rotation.Slots.WithheldNibble:x}] was requested {withheldChildRequests} times at the reverted pivot");
                    Assert.Equal(0, requestsBelowWithheldChild);
                });
        }

        [Fact]
        [Trait("Reproducer", "snap1-phase2-resume-extension-span")]
        public Task Given_Snap1Phase2ResumedFromACursorBehindItsLastTrieFlush_When_TheResumedRunWritesAnExtensionWhosePathSpanCoversAStoredBranch_Then_HealEndsMatchedWithEveryAccountReachable()
            => RunResumedExtensionSpanAsync(stopAtHealEntry: false, resume =>
            {
                Assert.True(resume.UnresolvableAtHeal != null, $"the resumed run never completed a matched heal; at heal entry: {resume.DescribeHealEntry()}; run: ran={resume.Result?.Ran} failure={resume.RunFailure?.Message}");
                Assert.True(resume.UnresolvableAtHeal.Count == 0,
                    $"heal reported matched=True at 0x{resume.TargetRoot.ToHex()} but the account trie is not walkable: {string.Join(" | ", resume.UnresolvableAtHeal)}; at heal entry: {resume.DescribeHealEntry()}");
                Assert.Equal(0, resume.Log.Count("snap.flat.reheal attempt="));
                Assert.True(resume.RunFailure == null, resume.RunFailure?.ToString());
                Assert.True(resume.Result.Ran, resume.Result.SkipReason);
            });

        [Fact]
        [Trait("Reproducer", "snap1-phase2-resume-extension-span")]
        public Task Given_TheResumedExtensionShape_When_Phase2EndsUnderSpanClearing_Then_TheStoredBranchInsideTheExtensionSpanIsAbsentBeforeHeal()
            => RunResumedExtensionSpanAsync(stopAtHealEntry: true, resume =>
                Assert.All(resume.Shape.SpannedPaths, path => Assert.True(resume.AtHealEntry[path.ToHex()] == null,
                    $"[{path.ToHex()}] inside the resumed root extension's span still holds a stored node at heal entry; at heal entry: {resume.DescribeHealEntry()}")));

        [Fact]
        public Task Given_ABranchStoredInsideThePathSpanOfAnExtensionTheHealCommits_When_TheHealProcessesThatExtension_Then_TheDanglingBranchIsDeleted()
            => WithHandBuiltTrieServerAsync(async peer =>
            {
                var (branchRoot, extensionRoot) = BuildBranchThenExtensionTries(peer);
                var branchPath = new byte[] { 0x1, 0x2 };
                await using var follower = HandBuiltTrieFollower.Open();

                Assert.True((await follower.HealAsync(peer, branchRoot)).Matched, "the follower never synced the trie that holds a branch at [1,2]");
                Assert.NotNull(follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, branchPath));

                Assert.True((await follower.HealAsync(peer, extensionRoot)).Matched, "the heal to the trie whose extension spans [1,2] did not converge");
                Assert.True(follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, branchPath) == null,
                    "the branch at [1,2] survived the heal of an extension at [1] whose path spans [1,2]");
            });

        [Fact]
        public Task Given_StoredNodesAtEveryInternalPathOfALongerExtensionWhoseChildIsAlreadyComplete_When_TheHealProcessesThatExtension_Then_EveryInternalPathIsDeletedAndTheChildIsNotRequested()
            => WithHandBuiltTrieServerAsync(async peer =>
            {
                var branchRoot = AccountTrie(peer,
                    (AccountKey(0x1, 0x2, 0x3, 0x4, 0x0), AccountValue(1)),
                    (AccountKey(0x1, 0x2, 0x3, 0x4, 0x1), AccountValue(1)),
                    (AccountKey(0x1, 0x2, 0x3, 0x5), AccountValue(1)),
                    (AccountKey(0x1, 0x2, 0x5), AccountValue(1)),
                    (AccountKey(0x1, 0x5), AccountValue(1)),
                    (AccountKey(0x5), AccountValue(1)));
                var extensionRoot = AccountTrie(peer,
                    (AccountKey(0x1, 0x2, 0x3, 0x4, 0x0), AccountValue(1)),
                    (AccountKey(0x1, 0x2, 0x3, 0x4, 0x1), AccountValue(1)),
                    (AccountKey(0x5), AccountValue(2)));
                var extension = Assert.IsType<ExtendedNode>(ServerNodeAt(peer.Server, extensionRoot, new byte[] { 0x1 }));
                Assert.Equal(new byte[] { 0x2, 0x3, 0x4 }, extension.Nibbles);
                var child = Assert.IsType<HashNode>(extension.InnerNode);
                var childPath = new byte[] { 0x1, 0x2, 0x3, 0x4 };
                var internalPaths = new[] { new byte[] { 0x1, 0x2 }, new byte[] { 0x1, 0x2, 0x3 } };
                await using var follower = HandBuiltTrieFollower.Open();

                Assert.True((await follower.HealAsync(peer, branchRoot)).Matched, "the follower never synced the trie that holds branches at [1,2] and [1,2,3]");
                Assert.All(internalPaths, path => Assert.NotNull(follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path)));
                var childBefore = follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, childPath);
                Assert.Equal(child.Hash.ToHex(), HashOf(childBefore));

                var childRequests = 0;
                peer.Scheduler.WithholdTrieNodes((root, pathset) =>
                {
                    if (ByteUtil.AreEqual(root, extensionRoot) && pathset.Count == 1
                        && PatriciaPathWalker.CompactToNibbles(pathset[0]).SequenceEqual(childPath))
                        Interlocked.Increment(ref childRequests);
                    return false;
                });
                Assert.True((await follower.HealAsync(peer, extensionRoot)).Matched, "the heal to the trie whose extension spans [1,2] and [1,2,3] did not converge");

                Assert.All(internalPaths, path => Assert.True(follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path) == null,
                    $"the node at [{string.Join(",", path)}] survived the heal of an extension at [1] whose path spans it"));
                Assert.Equal(0, childRequests);
                Assert.Equal(childBefore, follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, childPath));
            });

        [Fact]
        public Task Given_ADanglingBranchLeftByAnEarlierExtension_When_ALaterPivotRestoresThatBranchWithTheSameHashButDifferentDescendants_Then_TheHealedTrieIsWalkable()
            => WithHandBuiltTrieServerAsync(async peer =>
            {
                var (branchRoot, extensionRoot) = BuildBranchThenExtensionTries(peer);
                await using var follower = HandBuiltTrieFollower.Open();

                Assert.True((await follower.HealAsync(peer, branchRoot)).Matched, "the follower never synced the trie that holds a branch at [1,2]");
                Assert.True((await follower.HealAsync(peer, extensionRoot)).Matched, "the heal to the trie whose extension spans [1,2] did not converge");
                var restored = await follower.HealAsync(peer, branchRoot);

                Assert.True(restored.Matched, "the heal back to the restored branch did not converge");
                Assert.Empty(StorageTriesUnresolvableAt(follower.Bundle, branchRoot));
            });

        [Fact]
        public Task Given_AnExtensionWhoseChildIsEmbedded_When_Healing_Then_NoInternalPathIsDeleted()
            => WithHandBuiltTrieServerAsync(async peer =>
            {
                var trie = new PatriciaTrie(peer.Server.Bundle.TrieNodes);
                var shared = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78, 0x9a };
                trie.Put(shared.Append((byte)0xb0).ToArray(), new byte[] { 0x01 });
                trie.Put(shared.Append((byte)0xb1).ToArray(), new byte[] { 0x02 });
                trie.Put(AccountKey(0x5, 0x0), AccountValue(1));
                trie.SaveNodesToStorage();
                var root = trie.Root.GetHash();
                Assert.IsType<HashNode>(Assert.IsType<BranchNode>(ServerNodeAt(peer.Server, root, Array.Empty<byte>())).Children[0x1]);
                var extension = Assert.IsType<ExtendedNode>(ServerNodeAt(peer.Server, root, new byte[] { 0x1 }));
                Assert.IsNotType<HashNode>(extension.InnerNode);

                await using var follower = HandBuiltTrieFollower.Open();
                var internalPath = new byte[] { 0x1, 0x2 };
                var (danglingBlob, danglingHash) = DanglingNode();
                follower.Manager.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, internalPath, danglingBlob);

                Assert.True((await follower.HealAsync(peer, root)).Matched, "the heal of the embedded-child extension trie did not converge");
                Assert.Equal(danglingHash.ToHex(), HashOf(follower.Manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, internalPath)));
            });

        private sealed record HandBuiltTriePeer(WireServerNode Server, ControlledSnapScheduler Scheduler);

        private async Task WithHandBuiltTrieServerAsync(Func<HandBuiltTriePeer, Task> scenario)
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync();
            await using var server = await WireServerNode.StartAsync(sequencer, idleTimeout: ServerIdleTimeout);
            var pool = await ConnectAsync(server, false, Microsoft.Extensions.Logging.Abstractions.NullLogger<PeerPoolManager>.Instance);
            try
            {
                var scheduler = new ControlledSnapScheduler(new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions()));
                await scenario(new HandBuiltTriePeer(server, scheduler));
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }

        private sealed class HandBuiltTrieFollower : IAsyncDisposable
        {
            private readonly string _path;

            private HandBuiltTrieFollower(string path, RocksDbManager manager, RocksDbChainStoreBundle bundle)
            {
                _path = path;
                Manager = manager;
                Bundle = bundle;
            }

            public RocksDbManager Manager { get; }
            public RocksDbChainStoreBundle Bundle { get; }

            public static HandBuiltTrieFollower Open()
            {
                var path = Path.Combine(Path.GetTempPath(), "wire-heal-trie-" + Guid.NewGuid().ToString("N"));
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = path, PathKeyedState = true });
                return new HandBuiltTrieFollower(path, manager, RocksDbChainStoreBundle.FromManager(manager, path));
            }

            public async Task<TrieHealer.HealResult> HealAsync(
                HandBuiltTriePeer peer, byte[] root,
                IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seeds = null, ulong pivotBlock = 0,
                Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>> pivotRefresher = null)
            {
                var healer = new TrieHealer(peer.Scheduler, ((IHealNodeSinkProvider)Bundle).CreateHealSink(), Bundle.StateTrieNodes)
                {
                    PivotRefresher = pivotRefresher,
                };
                using var cts = new CancellationTokenSource(StepTimeout);
                return await healer.HealAsync(root, seeds, pivotBlock: pivotBlock, ct: cts.Token);
            }

            public ValueTask DisposeAsync()
            {
                Bundle.Dispose();
                try { if (Directory.Exists(_path)) Directory.Delete(_path, true); } catch { }
                return ValueTask.CompletedTask;
            }
        }

        private static (byte[] BranchRoot, byte[] ExtensionRoot) BuildBranchThenExtensionTries(HandBuiltTriePeer peer)
        {
            var branchRoot = AccountTrie(peer,
                (AccountKey(0x1, 0x2, 0x3, 0x0), AccountValue(1)),
                (AccountKey(0x1, 0x2, 0x3, 0x1), AccountValue(1)),
                (AccountKey(0x1, 0x2, 0x4, 0x0), AccountValue(1)),
                (AccountKey(0x1, 0x5, 0x0), AccountValue(1)),
                (AccountKey(0x5, 0x0), AccountValue(1)));
            var extensionRoot = AccountTrie(peer,
                (AccountKey(0x1, 0x2, 0x3, 0x0), AccountValue(2)),
                (AccountKey(0x1, 0x2, 0x3, 0x1), AccountValue(2)),
                (AccountKey(0x5, 0x0), AccountValue(1)));
            Assert.IsType<BranchNode>(ServerNodeAt(peer.Server, branchRoot, new byte[] { 0x1, 0x2 }));
            var extension = Assert.IsType<ExtendedNode>(ServerNodeAt(peer.Server, extensionRoot, new byte[] { 0x1 }));
            Assert.Equal(new byte[] { 0x2, 0x3 }, extension.Nibbles);
            Assert.IsType<HashNode>(extension.InnerNode);
            return (branchRoot, extensionRoot);
        }

        private static byte[] AccountKey(params byte[] leadingNibbles)
        {
            var nibbles = new byte[64];
            leadingNibbles.CopyTo(nibbles, 0);
            for (var i = leadingNibbles.Length; i < 64; i++) nibbles[i] = (byte)((i * 7 + leadingNibbles.Length) & 0xf);
            var key = new byte[32];
            for (var i = 0; i < 32; i++) key[i] = (byte)((nibbles[2 * i] << 4) | nibbles[2 * i + 1]);
            return key;
        }

        private static byte[] AccountValue(int balance, byte[] storageRoot = null)
            => new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)balance,
                StateRoot = storageRoot ?? DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });

        private static byte[] AccountTrie(HandBuiltTriePeer peer, params (byte[] Key, byte[] Value)[] accounts)
        {
            var trie = new PatriciaTrie(peer.Server.Bundle.TrieNodes);
            foreach (var (key, value) in accounts) trie.Put(key, value);
            trie.SaveNodesToStorage();
            return trie.Root.GetHash();
        }

        private static byte[] StorageTrie(HandBuiltTriePeer peer, int b0, int b1, int f40, int f41)
        {
            var trie = new PatriciaTrie(peer.Server.Bundle.TrieNodes);
            trie.Put(AccountKey(0xb, 0x0), Nethereum.RLP.RLP.EncodeElement(new[] { (byte)b0 }));
            trie.Put(AccountKey(0xb, 0x1), Nethereum.RLP.RLP.EncodeElement(new[] { (byte)b1 }));
            trie.Put(AccountKey(0x4, 0x0), Nethereum.RLP.RLP.EncodeElement(new[] { (byte)f40 }));
            trie.Put(AccountKey(0x4, 0x1), Nethereum.RLP.RLP.EncodeElement(new[] { (byte)f41 }));
            trie.SaveNodesToStorage();
            return trie.Root.GetHash();
        }

        private static Node ServerNodeAt(WireServerNode server, byte[] root, byte[] nibblePath)
        {
            var node = (Node)new HashNode { Hash = root };
            var depth = 0;
            while (true)
            {
                if (node is HashNode hash)
                {
                    hash.DecodeInnerNode(server.Bundle.TrieNodes, false);
                    node = hash.InnerNode;
                }
                if (depth == nibblePath.Length) return node;
                switch (node)
                {
                    case BranchNode branch:
                        node = branch.Children[nibblePath[depth++]];
                        break;
                    case ExtendedNode ext:
                        depth += ext.Nibbles.Length;
                        node = ext.InnerNode;
                        break;
                    default:
                        return null;
                }
            }
        }

        private static (byte[] Blob, byte[] Hash) DanglingNode()
        {
            var blob = Enumerable.Repeat((byte)0xEE, 40).ToArray();
            return (blob, Sha3Keccack.Current.CalculateHash(blob));
        }

        private sealed class RewriteThenRevertRotation
        {
            public Live Live;
            public RevertingSlots Slots;
            public byte[] Owner;
            public byte[] RewrittenRoot;
            public byte[] RevertedRoot;
            public byte[] RewrittenStorageRoot;
            public byte[] RevertedStorageRoot;
            public CapturingLogger Log;
            public RocksDbManager Manager;
            public RocksDbChainStoreBundle Bundle;
            public Action<byte[], List<byte[]>> OnPathset;
            public string HealedTarget;
            public List<string> UnresolvableAtHeal;
            public string PathsAtHeal;
            public Dictionary<string, byte[]> OnDiskAtRetarget;
            public byte[] ServedChildAtRetarget;
            public SnapBootstrapper.Result Result;
            public Exception RunFailure;
        }

        private Task RunRewriteThenRevertRotationAsync(Action<RewriteThenRevertRotation> arrange, Action<RewriteThenRevertRotation> assert)
            => Observed(async () =>
            {
                var workload = await BuildWorkloadAsync(false, WhaleShape.Static, revertingStorage: true);
                await using var live = await StartLiveAsync(false, workload);
                var slots = ChooseRevertingSlots();
                var owner = Sha3Keccack.Current.CalculateHash(workload.RevertingStorageAddress.HexToByteArray());
                var phase2Pivot = live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance;
                var rewrittenPivot = live.StageTip(2) - SnapSyncOrchestrator.PivotServeTrailDistance;
                var revertedPivot = live.StageTip(3) - SnapSyncOrchestrator.PivotServeTrailDistance;
                var phase2Root = (await live.Server.Bundle.Blocks.GetByNumberAsync(phase2Pivot)).StateRoot;
                var rewrittenRoot = (await live.Server.Bundle.Blocks.GetByNumberAsync(rewrittenPivot)).StateRoot;
                var revertedRoot = (await live.Server.Bundle.Blocks.GetByNumberAsync(revertedPivot)).StateRoot;
                var phase2StorageRoot = ServerAccountAt(live, phase2Root, workload.RevertingStorageAddress).StateRoot;
                var rewrittenStorageRoot = ServerAccountAt(live, rewrittenRoot, workload.RevertingStorageAddress).StateRoot;
                var revertedStorageRoot = ServerAccountAt(live, revertedRoot, workload.RevertingStorageAddress).StateRoot;
                Assert.True(phase2Pivot < RevertingStorageChangeBlock && RevertingStorageChangeBlock <= rewrittenPivot
                    && rewrittenPivot < RevertingStorageRevertBlock && RevertingStorageRevertBlock <= revertedPivot,
                    $"the storage rewrite ({RevertingStorageChangeBlock}) and revert ({RevertingStorageRevertBlock}) do not straddle the pivots {phase2Pivot}/{rewrittenPivot}/{revertedPivot}");
                Assert.Equal(phase2StorageRoot.ToHex(), revertedStorageRoot.ToHex());
                Assert.NotEqual(phase2StorageRoot.ToHex(), rewrittenStorageRoot.ToHex());

                var rotation = new RewriteThenRevertRotation
                {
                    Live = live, Slots = slots, Owner = owner, RewrittenRoot = rewrittenRoot, RevertedRoot = revertedRoot,
                    RewrittenStorageRoot = rewrittenStorageRoot, RevertedStorageRoot = revertedStorageRoot, Log = live.NewLog(),
                };
                byte[] phase3EntryRoot = null;
                string phase3Entry = null;
                var revertStageReached = 0;
                live.On(m =>
                {
                    if (!StartsWith(m, "snap.phase.transition from=Phase2 to=Phase3") || Interlocked.CompareExchange(ref phase3Entry, m, null) != null) return;
                    Volatile.Write(ref phase3EntryRoot, RootAfter(m, "heal_target=0x"));
                    live.AdvanceStage();
                });
                live.Scheduler.WithholdTrieNodes((root, pathset) =>
                {
                    rotation.OnPathset?.Invoke(root, pathset);
                    var entryRoot = Volatile.Read(ref phase3EntryRoot);
                    if (entryRoot != null && ByteUtil.AreEqual(root, entryRoot)) return true;
                    if (!ByteUtil.AreEqual(root, rewrittenRoot) || pathset.Count != 2 || !ByteUtil.AreEqual(pathset[0], owner)) return false;
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[1]);
                    if (nibbles.Length == 0 || nibbles[0] != slots.WithheldNibble) return false;
                    if (Interlocked.Exchange(ref revertStageReached, 1) == 0) live.AdvanceStage();
                    return true;
                });

                await using (var bundleDb = live.Open())
                {
                    (rotation.Manager, rotation.Bundle) = bundleDb;
                    await rotation.Bundle.ResetSnapBootstrapStateAsync();
                    live.On(m =>
                    {
                        if (StartsWith(m, "Heal complete") && m.Contains("matched=True", StringComparison.Ordinal) && rotation.HealedTarget == null)
                            rotation.HealedTarget = RootAfter(m, "target=0x").ToHex();
                    });
                    arrange(rotation);

                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, rotation.Log, 1);
                    try { rotation.Result = await run; } catch (Exception ex) { rotation.RunFailure = ex; }

                    _out.WriteLine($"reverting contract 0x{owner.ToHex()} slots rewritten={slots.Rewritten} deleted={slots.Deleted} withheld={slots.Withheld} rewritten_nibble={slots.RewrittenNibble:x} withheld_nibble={slots.WithheldNibble:x}");
                    _out.WriteLine($"storage roots: phase2(blk {phase2Pivot})=0x{phase2StorageRoot.ToHex()} rewritten(blk {rewrittenPivot})=0x{rewrittenStorageRoot.ToHex()} reverted(blk {revertedPivot})=0x{revertedStorageRoot.ToHex()}");
                    _out.WriteLine($"phase3 entry: {phase3Entry}");
                    _out.WriteLine($"healed target=0x{rotation.HealedTarget} withheld_paths={live.Scheduler.WithheldTrieNodePaths}");
                    _out.WriteLine($"run: ran={rotation.Result?.Ran} pivot={rotation.Result?.PivotBlockNumber} failure={rotation.RunFailure?.GetType().Name}: {rotation.RunFailure?.Message}");

                    Assert.True(phase3Entry != null, "phase 2 never handed off to a trie-node heal");
                    Assert.Contains($"pivot={phase2Pivot} ", phase3Entry);
                    Assert.True(live.Scheduler.WithheldTrieNodePaths > 0 && Volatile.Read(ref revertStageReached) == 1,
                        "the heal never reached the rewritten storage trie at the rotated pivot, so no descendant could be rewritten ahead of its ancestor");
                    Assert.Contains(rotation.Log.Messages, m => MovesHealTo(m, rewrittenRoot));
                    Assert.Contains(rotation.Log.Messages, m => MovesHealTo(m, revertedRoot));
                    assert(rotation);
                }
            });

        private const int AccountPartitions = 16;
        private static readonly byte[] ExtensionSpanBucketTail = { 0xf, 0xf };
        private static readonly byte[] ExtensionSpanLeafNibbles = { 0x1, 0x5, 0x9 };

        private sealed record ExtensionSpanShape(byte[] Bucket, byte[][] AccountHashes)
        {
            public byte[] Cursor => AccountHashes[1];
            public IEnumerable<byte[]> SpannedPaths => Enumerable.Range(1, Bucket.Length - 1).Select(i => Bucket.Take(i).ToArray());
            public IEnumerable<byte[]> WatchedPaths => SpannedPaths.Append(Bucket);
        }

        private sealed class ResumedExtensionSpan
        {
            public Live Live;
            public ExtensionSpanShape Shape;
            public byte[] TargetRoot;
            public ulong Pivot;
            public byte[] PivotHash;
            public CapturingLogger Log;
            public Dictionary<string, byte[]> AtHealEntry;
            public Dictionary<string, byte[]> TargetReferences;
            public List<string> UnresolvableAtHeal;
            public SnapBootstrapper.Result Result;
            public Exception RunFailure;

            public string DescribeHealEntry()
                => AtHealEntry == null
                    ? "heal never entered"
                    : string.Join("; ", Shape.WatchedPaths.Select(p => $"[{p.ToHex()}] local=0x{HashOf(AtHealEntry[p.ToHex()])} target=0x{TargetReferences[p.ToHex()].ToHex()}"));
        }

        private Task RunResumedExtensionSpanAsync(bool stopAtHealEntry, Action<ResumedExtensionSpan> assert)
            => Observed(async () =>
            {
                var workload = await BuildWorkloadAsync(false);
                var shape = await FundExtensionSpanAccountsAsync(workload);
                await using var live = await StartLiveAsync(false, workload, staticTipAtHead: true);
                var resume = new ResumedExtensionSpan { Live = live, Shape = shape };
                await RunPhase2AndStopBeforeThePivotIsCommittedAsync(resume);
                AssertBucketHoldsExactlyTheShapeAccounts(resume);
                await ResumeFromTheRewoundCursorAsync(resume, stopAtHealEntry);
                assert(resume);
            });

        private static async Task<ExtensionSpanShape> FundExtensionSpanAccountsAsync(Workload workload)
        {
            var sequencer = workload.Sequencer;
            var head = await sequencer.Blocks.GetByNumberAsync(await sequencer.Blocks.GetHeightAsync());
            var occupied = AccountHashesAt(sequencer.TrieNodes, head.StateRoot);
            var bucket = Enumerable.Range(0, 16)
                .Select(t => new[] { (byte)t }.Concat(ExtensionSpanBucketTail).ToArray())
                .FirstOrDefault(b => !occupied.Any(h => HasNibblePrefix(h, b)));
            Assert.True(bucket != null,
                $"every bucket [t,{ExtensionSpanBucketTail.ToHex()}] already holds server accounts: {DescribeBucketOccupancy(occupied)}");

            var funder = sequencer.Accounts.All[TouchedAccounts + 4];
            var addresses = ExtensionSpanLeafNibbles.Select(n => GrindAddressUnder(bucket.Append(n).ToArray())).ToArray();
            foreach (var address in addresses) sequencer.QueueTransfer(funder, address, Eth(1));
            await ProduceSucceededAsync(sequencer, "extension span accounts funding");
            for (ulong i = 0; i < SnapSyncOrchestrator.PivotServeTrailDistance; i++)
            {
                sequencer.QueueTransfer(funder, sequencer.Accounts.All[TouchedAccounts + 5].Address, Eth(1));
                await ProduceSucceededAsync(sequencer, "pivot trail");
            }
            return new ExtensionSpanShape(bucket, addresses.Select(a => Sha3Keccack.Current.CalculateHash(a.HexToByteArray())).ToArray());
        }

        private static string GrindAddressUnder(byte[] nibblePrefix)
        {
            for (var i = 0; ; i++)
            {
                var address = "0x5a" + i.ToString("x").PadLeft(38, '0');
                if (HasNibblePrefix(Sha3Keccack.Current.CalculateHash(address.HexToByteArray()), nibblePrefix)) return address;
            }
        }

        private static bool HasNibblePrefix(byte[] hash, byte[] nibblePrefix)
        {
            var nibbles = hash.ConvertToNibbles();
            return nibbles.Take(nibblePrefix.Length).SequenceEqual(nibblePrefix);
        }

        private static List<byte[]> AccountHashesAt(ITrieNodeStore store, byte[] stateRoot)
            => PatriciaRangeIterator.EnumerateRange(PatriciaTrie.LoadFromStorage(stateRoot, store).Root, store, new byte[32])
                .Select(leaf => leaf.KeyBytes).ToList();

        private static string DescribeBucketOccupancy(List<byte[]> accountHashes)
            => string.Join(" ", Enumerable.Range(0, 16).Select(t =>
                $"[{t:x}{ExtensionSpanBucketTail.ToHex()}]={accountHashes.Count(h => HasNibblePrefix(h, new[] { (byte)t }.Concat(ExtensionSpanBucketTail).ToArray()))}"));

        private static async Task RunPhase2AndStopBeforeThePivotIsCommittedAsync(ResumedExtensionSpan resume)
        {
            var live = resume.Live;
            var log = live.NewLog();
            var phase2Ended = live.DbPath + "-phase2-ended";
            string populated = null;
            SnapBootstrapper.Result stopped;
            await using (var running = live.Open())
            {
                await running.Bundle.ResetSnapBootstrapStateAsync();
                using var cts = new CancellationTokenSource(RunTimeout);
                live.On(m =>
                {
                    if (!StartsWith(m, "Snap-bootstrap: state populated") || Interlocked.CompareExchange(ref populated, m, null) != null) return;
                    running.Manager.CreateDatabaseCheckpoint(phase2Ended);
                    cts.Cancel();
                });
                stopped = await StopAsync(live.RunAsync(running, cts.Token));
            }
            Directory.Delete(live.DbPath, true);
            Directory.Move(phase2Ended, live.DbPath);
            await using var bundleDb = live.Open();
            var (manager, bundle) = bundleDb;

            Assert.True(populated != null, "run 1 never finished Phase 2");
            Assert.Contains("computed root matches pivot: True", populated);
            Assert.False(stopped?.Ran ?? false, "run 1 committed its pivot before it could be stopped");
            Assert.Equal(0, log.Count("snap.phase.transition from=Phase2 to=Phase3"));
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());
            var saved = bundle.Metadata.GetSnapSyncState();
            Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
            resume.Pivot = saved.PivotBlockNumber;
            resume.PivotHash = saved.PivotBlockHash;
            resume.TargetRoot = (await live.Server.Bundle.Blocks.GetByNumberAsync(saved.PivotBlockNumber)).StateRoot;
            Assert.Equal(resume.TargetRoot.ToHex(), HashOf(manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>())));
            AssertRunOneStoredTheTargetNodesUnderTheLaterSpan(resume, manager);
        }

        private static void AssertRunOneStoredTheTargetNodesUnderTheLaterSpan(ResumedExtensionSpan resume, RocksDbManager manager)
            => Assert.All(resume.Shape.WatchedPaths, path =>
            {
                var target = ServerNodeAt(resume.Live.Server, resume.TargetRoot, path).GetHash().ToHex();
                var stored = HashOf(manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
                Assert.True(stored == target, $"run 1 left [{path.ToHex()}] as 0x{stored}, not the target's 0x{target}");
            });

        private static void AssertBucketHoldsExactlyTheShapeAccounts(ResumedExtensionSpan resume)
        {
            var accounts = AccountHashesAt(resume.Live.Server.Bundle.TrieNodes, resume.TargetRoot);
            var inBucket = accounts.Where(h => HasNibblePrefix(h, resume.Shape.Bucket)).Select(h => h.ToHex()).OrderBy(h => h).ToList();
            Assert.Equal(resume.Shape.AccountHashes.Select(h => h.ToHex()).OrderBy(h => h), inBucket);
            var leafNibbles = resume.Shape.AccountHashes.Select(h => h.ConvertToNibbles()[resume.Shape.Bucket.Length]).ToList();
            Assert.Equal(leafNibbles.Count, leafNibbles.Distinct().Count());
            var partition = new[] { resume.Shape.Bucket[0] };
            var atOrAfterCursor = accounts
                .Where(h => HasNibblePrefix(h, partition) && h.ToHex().CompareTo(resume.Shape.Cursor.ToHex()) >= 0)
                .Select(h => h.ToHex()).OrderBy(h => h).ToList();
            Assert.Equal(resume.Shape.AccountHashes.Skip(1).Select(h => h.ToHex()).OrderBy(h => h), atOrAfterCursor);
        }

        private async Task ResumeFromTheRewoundCursorAsync(ResumedExtensionSpan resume, bool stopAtHealEntry)
        {
            var live = resume.Live;
            resume.Log = live.NewLog();
            await using var bundleDb = live.Open();
            var (manager, bundle) = bundleDb;
            _out.WriteLine($"run-1 completion artefacts before run 2: {CompletionArtefacts(bundle)}");
            bundle.Metadata.SaveSnapSyncState(Phase2StateWithCursorRewoundTo(resume.Pivot, resume.PivotHash, resume.Shape.Cursor));
            resume.TargetReferences = resume.Shape.WatchedPaths.ToDictionary(p => p.ToHex(), p => ServerNodeAt(live.Server, resume.TargetRoot, p).GetHash());

            using var cts = new CancellationTokenSource(RunTimeout);
            live.On(m =>
            {
                if (!StartsWith(m, "Snap leaf-stream root mismatch — entering heal phase") || resume.AtHealEntry != null) return;
                resume.AtHealEntry = resume.Shape.WatchedPaths.ToDictionary(p => p.ToHex(), p => manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, p));
                if (stopAtHealEntry) cts.Cancel();
            });
            live.On(m =>
            {
                if (!StartsWith(m, "Heal complete") || !m.Contains("matched=True", StringComparison.Ordinal) || resume.UnresolvableAtHeal != null) return;
                resume.UnresolvableAtHeal = StorageTriesUnresolvableAt(bundle, RootAfter(m, "target=0x"));
                if (resume.UnresolvableAtHeal.Count > 0) cts.Cancel();
            });

            var run = live.RunAsync(bundleDb, cts.Token);
            try { resume.Result = await StopAsync(run); } catch (Exception ex) { resume.RunFailure = ex; }

            _out.WriteLine($"bucket [{resume.Shape.Bucket.ToHex()}] accounts {string.Join(",", resume.Shape.AccountHashes.Select(h => "0x" + h.ToHex()))} cursor=0x{resume.Shape.Cursor.ToHex()} pivot={resume.Pivot} target=0x{resume.TargetRoot.ToHex()}");
            _out.WriteLine($"at heal entry: {resume.DescribeHealEntry()}");
            foreach (var u in resume.UnresolvableAtHeal ?? new List<string>()) _out.WriteLine($"unresolvable at heal completion: {u}");

            Assert.True(resume.Log.Count("snap.bootstrap.resume phase=Phase2") > 0, "run 2 did not resume Phase 2 from the written state");
            Assert.True(resume.AtHealEntry != null, "the resumed Phase 2 never handed off to heal");
            AssertTheResumedRunWroteAPartialBucketUnderASpanHoldingNoForeignNode(resume);
        }

        private static void AssertTheResumedRunWroteAPartialBucketUnderASpanHoldingNoForeignNode(ResumedExtensionSpan resume)
        {
            var bucket = resume.Shape.Bucket.ToHex();
            Assert.True(resume.AtHealEntry[bucket] != null && HashOf(resume.AtHealEntry[bucket]) != resume.TargetReferences[bucket].ToHex(),
                $"the resumed run did not write a partial node at [{bucket}]; at heal entry: {resume.DescribeHealEntry()}");
            Assert.All(resume.Shape.SpannedPaths.Select(p => p.ToHex()), p => Assert.True(
                resume.AtHealEntry[p] == null || HashOf(resume.AtHealEntry[p]) == resume.TargetReferences[p].ToHex(),
                $"[{p}] holds a node that is neither absent nor the target's; at heal entry: {resume.DescribeHealEntry()}"));
        }

        private static string CompletionArtefacts(RocksDbChainStoreBundle bundle)
        {
            var reconciler = (IFlatStateReconciler)bundle;
            return $"committed_head={bundle.Metadata.GetLastBlock()} snap_state_phase={bundle.Metadata.GetSnapSyncState()?.Phase.ToString() ?? "none"} " +
                   $"flat_damage={reconciler.GetPersistedDamage().Count} missing_code={reconciler.GetPersistedMissingCode().Count} " +
                   $"deferred_heal_code_bytes={bundle.Metadata.GetDeferredHealCodeBlob()?.Length ?? 0} deferred_heal_accounts_bytes={bundle.Metadata.GetDeferredHealAccountsBlob()?.Length ?? 0} " +
                   $"open_storage_debts={bundle.Metadata.CountOpenDeferredStorageDebts()}";
        }

        private static SnapSyncState Phase2StateWithCursorRewoundTo(ulong pivot, byte[] pivotHash, byte[] cursor)
            => new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = pivot,
                PivotBlockHash = pivotHash,
                HealTargetRoot = new byte[32],
                Counters = SnapSyncCounters.Zero,
                Tasks = new SnapTaskSet(AccountPartitions).Tasks.Select(t => new SnapSyncAccountTask
                {
                    Next = Covers(t, cursor) ? cursor : t.Last,
                    Last = t.Last,
                    StorageCompleted = Array.Empty<byte[]>(),
                    SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                }).ToList(),
            };

        private static bool Covers(SnapAccountTask task, byte[] hash)
            => task.Next.ToHex().CompareTo(hash.ToHex()) <= 0 && hash.ToHex().CompareTo(task.Last.ToHex()) <= 0;

        private static bool MovesHealTo(string message, byte[] root)
            => message.Contains("signalling retarget to 0x" + root.ToHex(), StringComparison.Ordinal)
               || (message.Contains("snap.phase3.recycle", StringComparison.Ordinal) && message.Contains("root 0x" + root.ToHex(), StringComparison.Ordinal));

        private static byte[] AccountLeafPath(Live live, byte[] stateRoot, byte[] accountHash)
        {
            var key = accountHash.ConvertToNibbles();
            var path = new List<byte>();
            var root = new HashNode { Hash = stateRoot };
            root.DecodeInnerNode(live.Server.Bundle.TrieNodes, false);
            var node = root.InnerNode;
            while (true)
            {
                if (node is HashNode hash)
                {
                    hash.DecodeInnerNode(live.Server.Bundle.TrieNodes, false);
                    node = hash.InnerNode;
                }
                switch (node)
                {
                    case BranchNode branch:
                        node = branch.Children[key[path.Count]];
                        path.Add(key[path.Count]);
                        break;
                    case ExtendedNode ext:
                        path.AddRange(ext.Nibbles);
                        node = ext.InnerNode;
                        break;
                    case LeafNode:
                        return path.ToArray();
                    default:
                        throw new Xunit.Sdk.XunitException($"account 0x{accountHash.ToHex()} has no leaf under 0x{stateRoot.ToHex()}");
                }
            }
        }

        [Fact]
        [Trait("Reproducer", "mainnet-snap1-heal-zero-work-livelock")]
        public Task Given_Snap1HealTargetRootThatNoPeerServes_When_ThePivotRefreshKeepsReturningThatSameRoot_Then_HealCompletesFailsOrReseedsInsteadOfRecyclingWithZeroWork()
            => Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static));
                var log = live.NewLog();
                byte[] unservedRoot = null;
                var phase3Entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                live.On(m =>
                {
                    if (!StartsWith(m, "snap.phase.transition from=Phase2 to=Phase3")) return;
                    Interlocked.CompareExchange(ref unservedRoot, RootAfter(m, "heal_target=0x"), null);
                    phase3Entered.TrySetResult(m);
                });
                live.Scheduler.WithholdTrieNodes((root, _) =>
                {
                    var unserved = Volatile.Read(ref unservedRoot);
                    return unserved != null && ByteUtil.AreEqual(root, unserved);
                });

                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 1);
                    Assert.True(await Task.WhenAny(phase3Entered.Task, Task.Delay(StepTimeout)) == phase3Entered.Task,
                        "phase 2 never handed off to a trie-node heal");
                    var settled = await Task.WhenAny(run, Task.Delay(LivelockWindow)) == run;

                    var zeroWorkRecycles = log.Messages.Count(m => m.Contains("snap.phase3.recycle cycle=", StringComparison.Ordinal)
                        && m.Contains("cumulative 0 nodes", StringComparison.Ordinal)
                        && m.Contains("pruned=0 absent=0 stale=0", StringComparison.Ordinal));
                    var sameRootStalls = log.Count("pivot refresh returned same root");
                    _out.WriteLine($"phase3 entry: {phase3Entered.Task.Result}");
                    _out.WriteLine($"after {LivelockWindow.TotalSeconds:0}s: settled={settled} zero_work_recycles={zeroWorkRecycles} same_root_stalls={sameRootStalls} withheld_paths={live.Scheduler.WithheldTrieNodePaths} trie_node_requests={live.Scheduler.TrieNodeRequests} stage={live.CurrentStage}");
                    foreach (var m in log.Messages.Where(m => m.Contains("snap.phase3.recycle", StringComparison.Ordinal) || m.Contains("snap.phase3.stalled", StringComparison.Ordinal)).Take(6))
                        _out.WriteLine(m);
                    if (!settled)
                    {
                        cts.Cancel();
                        try { await run; } catch { }
                    }

                    Assert.True(live.Scheduler.WithheldTrieNodePaths > 0, "the heal never asked for the unserved root, so nothing was withheld");
                    Assert.True(settled || zeroWorkRecycles < MaxZeroWorkRecycles,
                        $"heal spun {zeroWorkRecycles} zero-work recycle cycles ({sameRootStalls} same-root stalls) in {LivelockWindow.TotalSeconds:0}s against a root no peer serves, " +
                        "never completing, failing or re-seeding");
                }
            });

        [Fact]
        public Task Given_Snap1HealWaitingOnARootNoPeerServes_When_TheTipAdvancesToAServedPivot_Then_TheHealRetargetsWithoutABootstrapRetryAndCompletesMatched()
            => HealAgainstAnUnservedEntryRootAsync(_ => true, async (live, log, unservedRoot, run) =>
            {
                Assert.True(await WaitUntilAsync(() => log.Count(WaitingForPivot) > 0, StepTimeout),
                    "the heal never started waiting on the root no peer serves");
                Assert.Equal(0, log.Count("snap.phase3.recycle cycle="));

                live.AdvanceStage();
                Assert.True(await WaitUntilAsync(() => MatchedHealsAwayFrom(log, unservedRoot) > 0, StepTimeout),
                    "the heal did not retarget to the advanced pivot and complete matched");
                Assert.True(await Task.WhenAny(run, Task.Delay(StepTimeout)) == run, "the sync did not finish after the heal retargeted");
                var result = await CompletedAsync(run);

                Assert.NotEqual(unservedRoot.ToHex(), result.PivotStateRoot.ToHex());
                Assert.Equal(0, log.Count("snap.bootstrap.retry"));
            });

        [Fact]
        public Task Given_Snap1HealWaitingOnARootNoPeerServes_When_NeitherTheRootChangesNorAPeerJoins_Then_NoTrieNodeRequestForThatRootIsSentAfterTheFirstStall()
            => HealAgainstAnUnservedEntryRootAsync(_ => true, async (live, log, unservedRoot, run) =>
            {
                Assert.True(await WaitUntilAsync(() => log.Count(WaitingForPivot) > 0, StepTimeout),
                    "the heal never started waiting on the root no peer serves");
                var requestsWhenWaiting = live.Scheduler.TrieNodeRequests;
                var withheldWhenWaiting = live.Scheduler.WithheldTrieNodePaths;
                Assert.True(withheldWhenWaiting > 0, "the heal never asked for the unserved root, so nothing was withheld");

                await Task.Delay(QuietWindow);

                var rejoins = log.Count("snap.phase3.peer_joined");
                Assert.False(run.IsCompleted, "the run ended while no new pivot or peer could have unblocked it");
                Assert.Equal(1 + rejoins, log.Count("snap.phase3.stalled"));
                Assert.Equal(requestsWhenWaiting * (1 + rejoins), live.Scheduler.TrieNodeRequests);
                Assert.Equal(withheldWhenWaiting * (1 + rejoins), live.Scheduler.WithheldTrieNodePaths);
            });

        [Fact]
        public Task Given_ASameRootHealCycleThatBankedNodesButDidNotConverge_When_TheCycleEnds_Then_TheNextCycleStartsWithoutWaiting()
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
                        await WaitUntilAsync(() => log1.Count("from=Phase2 to=Phase3") > 0 && live.Scheduler.TrieNodeRequests > 0, StepTimeout),
                        "phase 2 never handed off to a trie-node heal");
                    cts.Cancel();
                    var stopped = await StopAsync(run1);
                    Assert.False(stopped?.Ran ?? false, "the heal finished before it could be stopped");
                    saved = bundle1.Metadata.GetSnapSyncState();
                    Assert.Equal(SnapPhase.Phase3Running, saved.Phase);
                }

                var (withheld, served) = await EvictTwoDepthOneAccountNodesAsync(live, saved.HealTargetRoot);
                _out.WriteLine($"evicted depth-1 account nodes {withheld:x} (withheld) and {served:x} (served) under 0x{saved.HealTargetRoot.ToHex()}");
                live.Scheduler.WithholdTrieNodes((root, path) => ByteUtil.AreEqual(root, saved.HealTargetRoot) && FirstAccountNibble(path) == withheld);
                live.Scheduler.ReleaseTrieNodeRequests();

                var log = live.NewLog();
                await using (var bundle2Db = live.Open())
                {
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundle2Db, cts.Token);
                    try
                    {
                        Assert.True(await WaitUntilAsync(() => log.Count(WaitingForPivot) > 0, StepTimeout),
                            "the heal never started waiting after a cycle that banked nothing");
                        Assert.Contains(log.Messages, m => m.Contains("heal_target=0x" + saved.HealTargetRoot.ToHex(), StringComparison.OrdinalIgnoreCase));
                        var messages = log.Messages.ToList();
                        var stalls = IndexesOf(messages, "snap.phase3.stalled");
                        var firstRecycle = messages.FindIndex(m => m.Contains("snap.phase3.recycle cycle=1 ", StringComparison.Ordinal));
                        var firstWait = messages.FindIndex(m => m.Contains(WaitingForPivot, StringComparison.Ordinal));

                        Assert.True(firstRecycle >= 0, "the first cycle never ended in a recycle");
                        var banked = long.Parse(Regex.Match(messages[firstRecycle], @"cumulative (\d+) nodes").Groups[1].Value);
                        Assert.True(banked > 0, $"the first cycle banked no nodes although the evicted node at {served:x} was served: {messages[firstRecycle]}");
                        Assert.True(stalls.Count >= 2, $"only {stalls.Count} stalled cycle(s) before the wait");
                        Assert.True(stalls[0] < firstRecycle && firstRecycle < stalls[1] && stalls[1] < firstWait,
                            $"order stall1={stalls[0]} recycle1={firstRecycle} stall2={stalls[1]} wait={firstWait}: the cycle that banked {banked} nodes must be followed by another cycle without waiting");
                    }
                    finally
                    {
                        if (!run.IsCompleted)
                        {
                            cts.Cancel();
                            try { await run; } catch { }
                        }
                    }
                }
            });

        private static async Task<(int Withheld, int Served)> EvictTwoDepthOneAccountNodesAsync(Live live, byte[] healTarget)
        {
            var targetRoot = new HashNode { Hash = healTarget };
            targetRoot.DecodeInnerNode(live.Server.Bundle.TrieNodes, false);
            var targetBranch = Assert.IsType<BranchNode>(targetRoot.InnerNode);
            var evicted = new List<int>();
            await using (var bundleDb = live.Open())
            {
                var (manager, _) = bundleDb;
                for (var nibble = 0; nibble < 16 && evicted.Count < 2; nibble++)
                {
                    if (targetBranch.Children[nibble] is not HashNode) continue;
                    var path = new[] { (byte)nibble };
                    if (manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path) == null) continue;
                    manager.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
                    Assert.Null(manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
                    evicted.Add(nibble);
                }
            }
            Assert.True(evicted.Count == 2, $"only {evicted.Count} depth-1 account node(s) were stored under the heal target");
            return (evicted[0], evicted[1]);
        }

        private const string WaitingForPivot = "snap.phase3.waiting_for_pivot";
        private static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(90);

        private async Task HealAgainstAnUnservedEntryRootAsync(
            Func<List<byte[]>, bool> withheldAtTheEntryRoot,
            Func<Live, CapturingLogger, byte[], Task<SnapBootstrapper.Result>, Task> scenario)
            => await Observed(async () =>
            {
                await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static));
                var log = live.NewLog();
                byte[] unservedRoot = null;
                var phase3Entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                live.On(m =>
                {
                    if (!StartsWith(m, "snap.phase.transition from=Phase2 to=Phase3")) return;
                    Interlocked.CompareExchange(ref unservedRoot, RootAfter(m, "heal_target=0x"), null);
                    phase3Entered.TrySetResult(m);
                });
                live.Scheduler.WithholdTrieNodes((root, path) =>
                {
                    var unserved = Volatile.Read(ref unservedRoot);
                    return unserved != null && ByteUtil.AreEqual(root, unserved) && withheldAtTheEntryRoot(path);
                });

                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    try
                    {
                        await ForceMovesAsync(live, log, 1);
                        Assert.True(await Task.WhenAny(phase3Entered.Task, Task.Delay(StepTimeout)) == phase3Entered.Task,
                            "phase 2 never handed off to a trie-node heal");
                        await scenario(live, log, Volatile.Read(ref unservedRoot), run);
                    }
                    finally
                    {
                        if (!run.IsCompleted)
                        {
                            cts.Cancel();
                            try { await run; } catch { }
                        }
                    }
                }
            });

        private static int MatchedHealsAwayFrom(CapturingLogger log, byte[] root)
            => log.Messages.Count(m => m.Contains("Heal complete after", StringComparison.Ordinal)
                && m.Contains("matched=True", StringComparison.Ordinal)
                && !m.Contains(root.ToHex(), StringComparison.OrdinalIgnoreCase));

        private static List<int> IndexesOf(List<string> messages, string fragment)
            => messages.Select((m, i) => (m, i)).Where(x => x.m.Contains(fragment, StringComparison.Ordinal)).Select(x => x.i).ToList();

        private static int FirstAccountNibble(List<byte[]> pathset)
        {
            if (pathset.Count != 1 || pathset[0].Length == 0) return -1;
            var compact = pathset[0];
            if ((compact[0] & 0x10) != 0) return compact[0] & 0x0f;
            return compact.Length > 1 ? compact[1] >> 4 : -1;
        }

        private static readonly TimeSpan LivelockWindow = TimeSpan.FromSeconds(45);
        private const int MaxZeroWorkRecycles = 3;

        private static byte[] RootAfter(string message, string marker)
            => Regex.Match(message, Regex.Escape(marker) + "([0-9a-fA-F]{64})").Groups[1].Value.HexToByteArray();

        private static List<string> StorageTriesUnresolvableAt(RocksDbChainStoreBundle bundle, byte[] stateRoot)
        {
            var failures = new List<string>();
            var store = bundle.StateTrieNodes;
            var decoder = new AccountEncoder();
            try
            {
                foreach (var leaf in PatriciaRangeIterator.EnumerateRange(PatriciaTrie.LoadFromStorage(stateRoot, store).Root, store, new byte[32]))
                {
                    var account = decoder.Decode(leaf.Value);
                    if (account.StateRoot == null || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH)) continue;
                    try
                    {
                        var storage = PatriciaTrie.LoadFromStorage(account.StateRoot, store, leaf.KeyBytes);
                        foreach (var _ in PatriciaRangeIterator.EnumerateRange(storage.Root, store, new byte[32])) { }
                    }
                    catch (InvalidOperationException ex)
                    {
                        failures.Add($"account 0x{leaf.KeyBytes.ToHex()} storageRoot 0x{account.StateRoot.ToHex()}: {ex.Message}");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                failures.Add($"account trie 0x{stateRoot.ToHex()}: {ex.Message}");
            }
            return failures;
        }

        private static string DescribeStoragePaths(Live live, RocksDbManager manager, byte[] owner, byte[] expectedStorageRoot, byte[] rewrittenStorageRoot)
        {
            var expected = ServerBranch(live, expectedStorageRoot);
            var rewritten = ServerBranch(live, rewrittenStorageRoot);
            var localRoot = manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, owner);
            var parts = new List<string>
            {
                $"[] local=0x{HashOf(localRoot)} expected=0x{expectedStorageRoot.ToHex()} rewritten=0x{rewrittenStorageRoot.ToHex()}",
            };
            for (var nibble = 0; nibble < 16; nibble++)
            {
                var expectedChild = (expected.Children[nibble] as HashNode)?.Hash;
                var rewrittenChild = (rewritten.Children[nibble] as HashNode)?.Hash;
                if (expectedChild == null && rewrittenChild == null) continue;
                var local = manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(new[] { (byte)nibble }).ToArray());
                var localHash = HashOf(local);
                if (localHash == expectedChild?.ToHex()) continue;
                parts.Add($"[{nibble:x}] local=0x{localHash} expected=0x{expectedChild?.ToHex()} rewritten=0x{rewrittenChild?.ToHex()}");
            }
            return string.Join("; ", parts);
        }

        private static BranchNode ServerBranch(Live live, byte[] storageRoot)
        {
            var root = new HashNode { Hash = storageRoot };
            root.DecodeInnerNode(live.Server.Bundle.TrieNodes, false);
            return Assert.IsType<BranchNode>(root.InnerNode);
        }

        private static string HashOf(byte[] blob) => blob == null ? "absent" : Sha3Keccack.Current.CalculateHash(blob).ToHex();

        private async Task RunWhaleLifecycleScenarioAsync(bool snap2, WhaleShape whale, bool selfDestructLargeContract)
            => await Observed(async () =>
            {
                var workload = await BuildWorkloadAsync(snap2, whale, selfDestructLargeContract);
                await using var live = await StartLiveAsync(snap2, workload);
                var log = live.NewLog();
                await using (var bundleDb = live.Open())
                {
                    var (_, bundle) = bundleDb;
                    await bundle.ResetSnapBootstrapStateAsync();
                    using var cts = new CancellationTokenSource(RunTimeout);
                    var run = live.RunAsync(bundleDb, cts.Token);
                    await ForceMovesAsync(live, log, 2);
                    var result = await CompletedAsync(run);

                    AssertModeSpecificMechanism(live, result, minPivotMoves: 2);
                    var pivotHeader = await AssertSyncedStateAsync(live, bundle, result, cts.Token, minPivot: live.StageTip(1) - SnapSyncOrchestrator.PivotServeTrailDistance);

                    if (whale == WhaleShape.Emptied)
                    {
                        var serverWhaleSlots = await workload.Sequencer.State.GetAllStorageAsync(workload.WhaleAddress);
                        Assert.True(serverWhaleSlots.Count <= 5,
                            $"expected the workload to have emptied the whale; server still has {serverWhaleSlots.Count} slots");
                        var followerWhaleSlotsFlat = await bundle.State.GetAllStorageAsync(workload.WhaleAddress);
                        Assert.Equal(serverWhaleSlots.Count, followerWhaleSlotsFlat.Count);
                        Assert.Equal(ServerAccountAt(live, pivotHeader.StateRoot, workload.WhaleAddress).StateRoot.ToHex(), StorageRootOf(followerWhaleSlotsFlat).ToHex());
                    }

                    if (selfDestructLargeContract)
                    {
                        Assert.Null(await workload.Sequencer.State.GetAccountAsync(workload.LargeStorageContractAddress));
                        Assert.Null(await FollowerStateAtPivot.ReadAsync(bundle, pivotHeader.StateRoot, workload.LargeStorageContractAddress));
                        Assert.Empty(await bundle.State.GetAllStorageAsync(workload.LargeStorageContractAddress));
                    }
                }
            });

        private enum WhaleShape { Churned, Static, Emptied }

        private sealed record Workload(
            InProcessSequencerDriver Sequencer, string WhaleAddress, string LargeStorageContractAddress, string RevertingStorageAddress = null,
            MainnetShapedOwners MainnetShaped = null);

        private const int RevertingStorageSlots = 64;
        private const ulong RevertingStorageChangeBlock = 100;
        private const ulong RevertingStorageRevertBlock = 140;
        private const string SlotSetterRuntimeReturnHex = "67602035600035550060005260086018f3";

        private readonly record struct RevertingSlots(int Rewritten, int Deleted, int Withheld)
        {
            public int RewrittenNibble => FirstStorageNibble(Rewritten);
            public int WithheldNibble => FirstStorageNibble(Withheld);
        }

        private static byte[] StorageSlotKey(int slot)
        {
            var key = new byte[32];
            key[30] = (byte)(slot >> 8);
            key[31] = (byte)slot;
            return key;
        }

        private static int FirstStorageNibble(int slot) => Sha3Keccack.Current.CalculateHash(StorageSlotKey(slot))[0] >> 4;

        private static RevertingSlots ChooseRevertingSlots()
        {
            var byNibble = Enumerable.Range(0, RevertingStorageSlots).GroupBy(FirstStorageNibble).ToList();
            var shared = byNibble.First(g => g.Count() >= 3);
            var other = byNibble.First(g => g.Key != shared.Key && g.Count() >= 3);
            return new RevertingSlots(shared.ElementAt(0), shared.ElementAt(1), other.ElementAt(0));
        }

        private static byte[] SetSlotCallData(int slot, int value)
        {
            var data = new byte[64];
            StorageSlotKey(slot).CopyTo(data, 0);
            data[63] = (byte)value;
            return data;
        }

        private static string Hardfork(bool snap2) => snap2 ? "amsterdam" : "prague";

        private static string[] BulkEoaAddresses()
            => Enumerable.Range(0, BulkEoas).Select(i => "0xe" + i.ToString("x").PadLeft(39, '0')).ToArray();

        private static async Task<Workload> BuildWorkloadAsync(
            bool snap2, WhaleShape whale = WhaleShape.Churned, bool selfDestructLargeContract = false, bool revertingStorage = false,
            bool mainnetShaped = false)
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(
                generatedAccounts: ActorAccounts, extraPrefunded: BulkEoaAddresses(), hardfork: Hardfork(snap2),
                blockGasLimit: mainnetShaped ? MainnetShapedBlockGasLimit : null);
            var callGas = snap2 ? AmsterdamAirdropGas : PragueAirdropGas;
            var bulkEoas = BulkEoaAddresses();

            var whaleOwner = sequencer.Accounts.All[TouchedAccounts];
            var whaleAddress = sequencer.QueueDeploy(whaleOwner, LoadTestToken.Bytecode.HexToByteArray(), callGas);
            await ProduceSucceededAsync(sequencer, "whale deploy");
            for (var batch = 0; batch < WhaleAirdropBatches; batch++)
            {
                var airdrop = new AirdropFunction { StartIndex = batch * WhaleHoldersPerBatch, Count = WhaleHoldersPerBatch, Amount = 1_000 };
                sequencer.QueueCall(whaleOwner, whaleAddress, airdrop.GetCallData(), callGas);
                await ProduceSucceededAsync(sequencer, $"whale airdrop {batch}");
            }
            Assert.True((await sequencer.State.GetAllStorageAsync(whaleAddress)).Count >= WhaleHolders, "the airdrops did not grow the whale");

            if (whale == WhaleShape.Emptied)
            {
                for (var holder = 1; holder < WhaleHolders; holder++)
                {
                    sequencer.QueueCall(whaleOwner, whaleAddress, new MoveBetweenHoldersFunction { FromIndex = holder, ToIndex = 0, Amount = 1_000 }.GetCallData());
                    if (holder % 100 == 0) await ProduceSucceededAsync(sequencer, $"whale drain to {holder}");
                }
                await ProduceSucceededAsync(sequencer, "whale drain tail");
            }

            var storageWriter = sequencer.QueueDeploy(sequencer.Accounts.All[TouchedAccounts + 1], WorkloadContracts.CallableStorageLoggerBytecode);
            await ProduceSucceededAsync(sequencer, "storage writer deploy");

            string largeStorageAddress = null;
            if (selfDestructLargeContract)
            {
                var slots = snap2 ? AmsterdamSelfDestructedSlots : PragueSelfDestructedSlots;
                var bytecode = ("0x61" + slots.ToString("x4") + LargeStorageLoopHex + SelfDestructToOneHex).HexToByteArray();
                largeStorageAddress = sequencer.QueueDeploy(
                    sequencer.Accounts.All[TouchedAccounts + 2], bytecode,
                    snap2 ? AmsterdamLargeStorageDeployGas : PragueLargeStorageDeployGas);
                await ProduceSucceededAsync(sequencer, "large storage then self-destruct deploy");
            }

            string revertingStorageAddress = null;
            var revertingOwner = sequencer.Accounts.All[TouchedAccounts + 3];
            var reverting = ChooseRevertingSlots();
            if (revertingStorage)
            {
                var bytecode = ("0x61" + RevertingStorageSlots.ToString("x4") + LargeStorageLoopHex + SlotSetterRuntimeReturnHex).HexToByteArray();
                revertingStorageAddress = sequencer.QueueDeploy(revertingOwner, bytecode, PragueAirdropGas);
                await ProduceSucceededAsync(sequencer, "reverting storage deploy");
                Assert.Equal(RevertingStorageSlots, (await sequencer.State.GetAllStorageAsync(revertingStorageAddress)).Count);
            }
            var mainnetShapedOwners = mainnetShaped ? await DeployMainnetShapedOwnersAsync(sequencer) : null;

            for (var i = 0; i < ChurnBlocks; i++)
            {
                if (revertingStorageAddress != null)
                {
                    var next = (ulong)await sequencer.Blocks.GetHeightAsync() + 1;
                    if (next == RevertingStorageChangeBlock || next == RevertingStorageRevertBlock)
                    {
                        var changed = next == RevertingStorageChangeBlock;
                        sequencer.QueueCall(revertingOwner, revertingStorageAddress, SetSlotCallData(reverting.Rewritten, changed ? 2 : 1));
                        sequencer.QueueCall(revertingOwner, revertingStorageAddress, SetSlotCallData(reverting.Deleted, changed ? 0 : 1));
                        sequencer.QueueCall(revertingOwner, revertingStorageAddress, SetSlotCallData(reverting.Withheld, changed ? 2 : 1));
                    }
                }
                sequencer.QueueTransfer(sequencer.Accounts.All[i % TouchedAccounts], sequencer.Accounts.All[(i + 1) % TouchedAccounts].Address, Eth(1));
                sequencer.QueueCall(sequencer.Accounts.All[(i + 2) % TouchedAccounts], storageWriter, Array.Empty<byte>());
                for (var j = 0; j < BulkTransfersPerBlock; j++)
                    sequencer.QueueTransfer(
                        sequencer.Accounts.All[(i + 3 + j) % TouchedAccounts],
                        bulkEoas[(i * BulkTransfersPerBlock + j) * 37 % BulkEoas], Eth(1));
                if (whale == WhaleShape.Churned)
                {
                    sequencer.QueueCall(whaleOwner, whaleAddress, new ChurnFunction { StartIndex = (i * 7) % ChurnWindow, Count = 3, Amount = 1 }.GetCallData());
                    sequencer.QueueCall(whaleOwner, whaleAddress, new MoveBetweenHoldersFunction { FromIndex = WhaleHolders - 1 - i, ToIndex = 0, Amount = 1_000 }.GetCallData());
                }
                await sequencer.ProduceBlockAsync();
            }

            return new Workload(sequencer, whaleAddress, largeStorageAddress, revertingStorageAddress, mainnetShapedOwners);
        }

        private static async Task ProduceSucceededAsync(InProcessSequencerDriver sequencer, string what)
        {
            var block = await sequencer.ProduceBlockAsync();
            var receipts = await sequencer.Receipts.GetByBlockNumberAsync(block.Number);
            Assert.True(receipts.Count > 0, $"{what}: block {block.Number} carried no transaction");
            Assert.All(receipts, r => Assert.True(r.HasSucceeded == true, $"{what}: a transaction in block {block.Number} failed"));
        }

        private sealed class FollowerDb : IAsyncDisposable
        {
            private CancellationTokenSource _runCts;
            private Task<SnapBootstrapper.Result> _run;

            public FollowerDb(RocksDbManager manager, RocksDbChainStoreBundle bundle)
            {
                Manager = manager;
                Bundle = bundle;
            }

            public RocksDbManager Manager { get; }
            public RocksDbChainStoreBundle Bundle { get; }

            public void Deconstruct(out RocksDbManager manager, out RocksDbChainStoreBundle bundle)
            {
                manager = Manager;
                bundle = Bundle;
            }

            public Task<SnapBootstrapper.Result> Track(CancellationToken ct, Func<CancellationToken, Task<SnapBootstrapper.Result>> start)
            {
                _runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _run = start(_runCts.Token);
                return _run;
            }

            public async ValueTask DisposeAsync()
            {
                if (_run != null && !_run.IsCompleted)
                {
                    _runCts.Cancel();
                    try { await _run; } catch { }
                }
                _runCts?.Dispose();
                try
                {
                    Bundle.Dispose();
                }
                catch (SynchronizationLockException)
                {
                    Interlocked.Increment(ref DisposeRacedBackgroundWork);
                }
            }

            public static int DisposeRacedBackgroundWork;
        }

        private sealed class Live : IAsyncDisposable
        {
            private readonly object _handlersGate = new();
            private Action<string>[] _handlers = Array.Empty<Action<string>>();
            private readonly List<CapturingLogger> _logs = new();
            private readonly int[] _stage = new int[1];
            private CanonicalTip[] _tips;

            public int VerifiedPages;
            public LogRelay Relay { get; private init; }
            public Workload Workload { get; private init; }
            public WireServerNode Server { get; private init; }
            public PeerPoolManager Pool { get; private init; }
            public ControlledSnapScheduler Scheduler { get; private init; }
            public bool Snap2 { get; private init; }
            public bool PathKeyed { get; private init; }
            public int? LargeContractConcurrency { get; private init; }
            public ulong Head { get; private init; }
            public string DbPath { get; } = Path.Combine(Path.GetTempPath(), "wire-heal-" + Guid.NewGuid().ToString("N"));
            public SnapSyncMetrics Metrics { get; } = new();
            public IEnumerable<string> AllMessages => _logs.SelectMany(l => l.Messages);
            public CapturingLogger CurrentLog => _logs[^1];

            public static async Task<Live> StartAsync(
                bool snap2, Workload workload, bool pathKeyed = true, bool staticTipAtHead = false,
                int snapResponseLimit = SnapResponseLimit, int? largeContractConcurrency = null)
            {
                var server = snapResponseLimit > 0
                    ? await WireServerNode.StartAsync(workload.Sequencer, snapResponseLimit: snapResponseLimit, idleTimeout: ServerIdleTimeout, advertiseSnap2: snap2)
                    : await WireServerNode.StartAsync(workload.Sequencer, idleTimeout: ServerIdleTimeout, advertiseSnap2: snap2);
                var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
                var tipBlocks = staticTipAtHead
                    ? new[] { head }
                    : Enumerable.Range(0, Stages).Select(i => InitialPivot + (ulong)i * StageStep).Append(head).ToArray();
                if (!staticTipAtHead)
                    Assert.True(head > tipBlocks[Stages - 1] + StageStep, $"the workload head {head} leaves no room past the last stage");
                var tips = new CanonicalTip[tipBlocks.Length];
                for (var i = 0; i < tipBlocks.Length; i++) tips[i] = await TipAtAsync(server, tipBlocks[i]);

                var relay = new LogRelay();
                var pool = await ConnectAsync(server, snap2, new RelayLogger<PeerPoolManager>(relay));
                var scheduler = new ControlledSnapScheduler(new FetchRequestScheduler(
                    pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions(), logger: new RelayLogger<FetchRequestScheduler>(relay)));
                return new Live
                {
                    Relay = relay, Workload = workload, Server = server, Pool = pool, Scheduler = scheduler, Snap2 = snap2,
                    PathKeyed = pathKeyed, Head = head, LargeContractConcurrency = largeContractConcurrency, _tips = tips,
                };
            }

            public ulong StageTip(int stage) => _tips[Math.Min(stage, _tips.Length - 1)].BlockNumber;

            public void AdvanceStage() => Interlocked.Increment(ref _stage[0]);

            public int CurrentStage => Volatile.Read(ref _stage[0]);

            public void JumpToHead() => Volatile.Write(ref _stage[0], int.MaxValue);

            public CapturingLogger NewLog()
            {
                var log = new CapturingLogger { OnMessage = Dispatch };
                lock (_handlersGate) _handlers = Array.Empty<Action<string>>();
                _logs.Add(log);
                Relay.Target = log;
                Volatile.Write(ref VerifiedPages, 0);
                return log;
            }

            private void Dispatch(string message)
            {
                foreach (var handler in Volatile.Read(ref _handlers)) handler(message);
            }

            public Action<string> On(Action<string> handler)
            {
                lock (_handlersGate) _handlers = _handlers.Append(handler).ToArray();
                return handler;
            }

            public void Off(Action<string> handler)
            {
                lock (_handlersGate) _handlers = _handlers.Where(h => h != handler).ToArray();
            }

            public Task StopOnCrash(CancellationTokenSource cts)
            {
                var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                On(m =>
                {
                    if (!StartsWith(m, "snap.bootstrap.retry")) return;
                    cts.Cancel();
                    stopped.TrySetResult(true);
                });
                return stopped.Task;
            }

            public FollowerDb Open()
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = DbPath, PathKeyedState = PathKeyed });
                return new FollowerDb(manager, RocksDbChainStoreBundle.FromManager(manager, DbPath, journalOptions: HistoricalStateOptions.FullArchive));
            }

            public async Task EnsurePeerAsync()
            {
                if (Pool.ActivePeers.OfType<SyncPeerSession>().Any(IsServingPeer)) return;
                Pool.EnqueueCandidate(Server.Enode);
                Assert.True(
                    await WaitUntilAsync(() => Pool.ActivePeers.OfType<SyncPeerSession>().Any(IsServingPeer), TimeSpan.FromSeconds(60)),
                    "the server peer was lost and did not reconnect before the run");
            }

            private bool IsServingPeer(SyncPeerSession peer) => Snap2 ? peer.SupportsSnap2 : peer.SupportsSnap;

            public Task<SnapBootstrapper.Result> RunAsync(FollowerDb db, CancellationToken ct)
                => db.Track(ct, token => StartOrchestratorAsync(db, token));

            private async Task<SnapBootstrapper.Result> StartOrchestratorAsync(FollowerDb db, CancellationToken token)
            {
                await EnsurePeerAsync();
                return await SnapSyncOrchestrator.RunAsync(
                    db.Bundle, Pool, Scheduler,
                    new StagedTipSource(_tips, () => Volatile.Read(ref _stage[0])),
                    new FixedChainActivations(HardforkNames.Parse(Hardfork(Snap2))), CurrentLog,
                    new SnapSyncOrchestratorOptions
                    {
                        UseBackwardSkeleton = true,
                        RootRefreshIntervalMs = 50,
                        PivotStaleDistanceBlocks = StalePivotDistance,
                        LargeContractConcurrency = LargeContractConcurrency,
                        Metrics = Metrics,
                        BalHealEnabled = Snap2,
                    }, token);
            }

            public async ValueTask DisposeAsync()
            {
                await Pool.DisposeAsync();
                await Server.DisposeAsync();
                try { if (Directory.Exists(DbPath)) Directory.Delete(DbPath, true); } catch { }
            }
        }

        private Live _live;

        private async Task<Live> StartLiveAsync(
            bool snap2, Workload workload, bool pathKeyed = true, bool staticTipAtHead = false,
            int snapResponseLimit = SnapResponseLimit, int? largeContractConcurrency = null)
            => _live = await Live.StartAsync(snap2, workload, pathKeyed, staticTipAtHead, snapResponseLimit, largeContractConcurrency);

        private async Task Observed(Func<Task> scenario)
        {
            try
            {
                await scenario();
            }
            catch
            {
                foreach (var m in _live?.AllMessages.TakeLast(600) ?? Enumerable.Empty<string>()) _out.WriteLine(m);
                throw;
            }
            finally
            {
                if (_live != null)
                    _out.WriteLine($"scheduler: paused={_live.Scheduler.Paused} crashed={_live.Scheduler.Crashed} trie_node_requests={_live.Scheduler.TrieNodeRequests} stage={_live.CurrentStage} peers={_live.Pool.ActivePeers.Count} peer_removals={_live.AllMessages.Count(m => m.Contains("] peer removed", StringComparison.Ordinal))} dispose_races={FollowerDb.DisposeRacedBackgroundWork}");
            }
        }

        private static bool StartsWith(string message, string prefix) => message.StartsWith(prefix, StringComparison.Ordinal);

        private sealed class LogRelay
        {
            public volatile CapturingLogger Target;
        }

        private sealed class RelayLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
        {
            private readonly LogRelay _relay;
            public RelayLogger(LogRelay relay) => _relay = relay;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (IsEnabled(logLevel)) _relay.Target?.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        private static async Task<PeerPoolManager> ConnectAsync(WireServerNode server, bool snap2, Microsoft.Extensions.Logging.ILogger<PeerPoolManager> logger)
        {
            var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId, advertiseSnap2: snap2),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0),
                logger: logger,
                trustedDialKeys: new[] { server.Enode });
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => snap2 ? p.SupportsSnap2 : p.SupportsSnap),
                    TimeSpan.FromSeconds(30)),
                snap2 ? "no snap/2 peer" : "no snap peer");
            return pool;
        }

        private static int PivotMoves(IEnumerable<string> messages)
            => messages.Count(m => m.Contains("] snap.phase2.pivot_move", StringComparison.Ordinal));

        private static async Task ForceMovesAsync(Live live, CapturingLogger log, int moves)
        {
            for (var move = 1; move <= moves; move++)
            {
                var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var target = Volatile.Read(ref live.VerifiedPages) + PagesBeforeEachMove;
                var handler = live.On(m =>
                {
                    if (!StartsWith(m, "snap.verify") || Interlocked.Increment(ref live.VerifiedPages) < target) return;
                    live.Scheduler.PauseStateRequests();
                    paused.TrySetResult(true);
                });
                try
                {
                    Assert.True(await Task.WhenAny(paused.Task, Task.Delay(StepTimeout)) == paused.Task,
                        $"move {move} of {moves}: phase 2 never verified {PagesBeforeEachMove} more account pages");
                }
                finally
                {
                    live.Off(handler);
                }
                var before = PivotMoves(log.Messages);
                live.AdvanceStage();
                Assert.True(await WaitUntilAsync(() => PivotMoves(log.Messages) > before, StepTimeout),
                    $"phase-2 pivot move {move} of {moves} never happened");
                live.Scheduler.ResumeStateRequests();
            }
        }

        private static bool AdoptedAMovedTip(bool snap2, CapturingLogger log, ulong savedPivot)
            => snap2
                ? log.Count("snap.bal_catchup.applied") > 0
                : log.Count("snap.bootstrap.pivot_moved") > 0
                  || log.Count("snap.phase2.pivot_move new_root") > 0
                  || log.Messages
                      .Select(m => Regex.Match(m, @"\] snap\.pivot\.phase2_start block=(\d+)"))
                      .Any(m => m.Success && ulong.Parse(m.Groups[1].Value) > savedPivot);

        private static async Task<SnapBootstrapper.Result> CompletedAsync(Task<SnapBootstrapper.Result> run)
        {
            var result = await run;
            Assert.True(result.Ran, result.SkipReason);
            await result.HistoryBackfill;
            return result;
        }

        private static async Task<SnapBootstrapper.Result> StopAsync(Task<SnapBootstrapper.Result> run)
        {
            try { return await run; }
            catch (OperationCanceledException) { return null; }
        }

        private static async Task StoppedAsync(Task<SnapBootstrapper.Result> run, Task stopped, string failure)
        {
            Assert.True(await Task.WhenAny(stopped, run, Task.Delay(RunTimeout)) == stopped, failure);
            var result = await StopAsync(run);
            Assert.False(result?.Ran ?? false, "the stopped run reported a completed sync");
        }

        private static void AssertModeSpecificMechanism(Live live, SnapBootstrapper.Result result, int minPivotMoves)
        {
            var messages = live.AllMessages.ToList();
            var moves = PivotMoves(messages);
            var balHealed = messages.Count(m => m.Contains("] snap.phase2.pivot_move.bal_healed", StringComparison.Ordinal));
            var healEntered = messages.Any(m => m.Contains("entering heal phase", StringComparison.Ordinal));
            if (live.Snap2)
            {
                Assert.True(balHealed >= minPivotMoves, $"expected >= {minPivotMoves} BAL-closed pivot moves, saw {balHealed}");
                Assert.Equal(moves, balHealed);
                Assert.False(healEntered, "snap/2 must not enter trie-node heal");
                Assert.Equal(0L, live.Metrics.Phase3NodesHealedTotal);
                Assert.Equal(0, live.Scheduler.TrieNodeRequests);
                Assert.Equal(result.PivotStateRoot.ToHex(), GeneratedRoot(live.CurrentLog));
            }
            else
            {
                Assert.Equal(0, balHealed);
                Assert.True(moves >= minPivotMoves, $"expected >= {minPivotMoves} pivot moves, saw {moves}");
                Assert.True(healEntered, "snap/1 with a pivot moved across changed state must trie-heal");
                Assert.Contains(messages, m => m.Contains("Heal complete", StringComparison.Ordinal) && m.Contains("matched=True"));
            }
        }

        private static int ReconcileSlotsAdded(CapturingLogger log)
        {
            var line = log.Messages.LastOrDefault(m => m.Contains("snap.flat.reconcile done", StringComparison.Ordinal));
            Assert.True(line != null, "no 'snap.flat.reconcile done' line was logged — the flat state was not certified");
            var match = Regex.Match(line, @"added=(\d+)/(\d+)");
            Assert.True(match.Success, $"could not parse added=A/S from: {line}");
            return int.Parse(match.Groups[2].Value);
        }

        private static async Task<BlockHeader> AssertSyncedStateAsync(
            Live live, RocksDbChainStoreBundle bundle, SnapBootstrapper.Result result, CancellationToken ct, ulong minPivot)
        {
            var pivot = result.PivotBlockNumber;
            Assert.True(pivot >= minPivot, $"the final pivot {pivot} is below {minPivot}");
            var pivotHeader = await live.Server.Bundle.Blocks.GetByNumberAsync(pivot);
            Assert.Equal(pivotHeader.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
            var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, live.Server.Bundle.TrieNodes);
            var accountDecoder = new AccountEncoder();
            foreach (var account in live.Workload.Sequencer.Accounts.All.Take(TouchedAccounts))
            {
                var synced = await FollowerStateAtPivot.ReadAsync(bundle, pivotHeader.StateRoot, account.Address);
                Assert.NotNull(synced);
                var expectedLeaf = serverAtPivot.Get(Sha3Keccack.Current.CalculateHash(account.Address.HexToByteArray()));
                Assert.NotNull(expectedLeaf);
                Assert.Equal(accountDecoder.Decode(expectedLeaf).Balance, synced.Balance);
            }

            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            var reconciler = Assert.IsAssignableFrom<IFlatStateReconciler>(bundle);
            Assert.Empty(reconciler.GetPersistedDamage());
            var verify = await reconciler.VerifyFlatStateAsync(pivotHeader.StateRoot, null, ct);
            Assert.Equal(0, verify.TotalRepairs);

            if (live.Workload.WhaleAddress != null)
            {
                var flat = await bundle.State.GetAllStorageAsync(live.Workload.WhaleAddress);
                Assert.Equal(
                    ServerAccountAt(live, pivotHeader.StateRoot, live.Workload.WhaleAddress).StateRoot.ToHex(),
                    StorageRootOf(flat).ToHex());
            }
            return pivotHeader;
        }

        private static Account ServerAccountAt(Live live, byte[] stateRoot, string address)
        {
            var leaf = PatriciaTrie.LoadFromStorage(stateRoot, live.Server.Bundle.TrieNodes)
                .Get(Sha3Keccack.Current.CalculateHash(address.HexToByteArray()));
            Assert.NotNull(leaf);
            return new AccountEncoder().Decode(leaf);
        }

        private static byte[] StorageRootOf(Dictionary<byte[], byte[]> flatSlots)
        {
            if (flatSlots.Count == 0) return DefaultValues.EMPTY_TRIE_HASH;
            var trie = new PatriciaTrie();
            foreach (var kv in flatSlots)
                trie.Put(kv.Key, Nethereum.RLP.RLP.EncodeElement(kv.Value.SkipWhile(b => b == 0).ToArray()));
            return trie.Root.GetHash();
        }

        private static async Task<int> EvictAStaleDepthOneAccountNodeAsync(Live live, byte[] healTarget)
        {
            var targetRoot = new HashNode { Hash = healTarget };
            targetRoot.DecodeInnerNode(live.Server.Bundle.TrieNodes, false);
            var targetBranch = Assert.IsType<BranchNode>(targetRoot.InnerNode);

            await using (var bundleDb = live.Open())
            {
                var (manager, _) = bundleDb;
                var oldRoot = manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
                Assert.NotNull(oldRoot);
                Assert.NotEqual(healTarget.ToHex(), Sha3Keccack.Current.CalculateHash(oldRoot).ToHex());

                for (var nibble = 0; nibble < 16; nibble++)
                {
                    if (targetBranch.Children[nibble] is not HashNode targetChild) continue;
                    var path = new[] { (byte)nibble };
                    var local = manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
                    if (local == null || ByteUtil.AreEqual(Sha3Keccack.Current.CalculateHash(local), targetChild.Hash)) continue;

                    manager.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
                    Assert.Null(manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
                    return nibble;
                }
            }
            throw new Xunit.Sdk.XunitException("no depth-1 account node was replaced between the Phase-2 trie and the heal target; nothing to evict");
        }
    }
}
