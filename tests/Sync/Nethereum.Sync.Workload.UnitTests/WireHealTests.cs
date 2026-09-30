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
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Collection(Snap2LiveHarness.Collection)]
    public class WireHealTests
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
                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                foreach (var account in sequencer.Accounts.All.Take(TouchedAccounts))
                {
                    var synced = await recovered.GetAccountAsync(account.Address);
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
                    Assert.Equal(0, live.AllMessages.Count(m => m.Contains("snap.phase2.bigaccount.deferred", StringComparison.Ordinal)));
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
                    var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);

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
                        Assert.Null(await recovered.GetAccountAsync(workload.LargeStorageContractAddress));
                        Assert.Empty(await recovered.GetAllStorageAsync(workload.LargeStorageContractAddress));
                        Assert.Empty(await bundle.State.GetAllStorageAsync(workload.LargeStorageContractAddress));
                    }
                }
            });

        private enum WhaleShape { Churned, Static, Emptied }

        private sealed record Workload(InProcessSequencerDriver Sequencer, string WhaleAddress, string LargeStorageContractAddress);

        private static string Hardfork(bool snap2) => snap2 ? "amsterdam" : "prague";

        private static string[] BulkEoaAddresses()
            => Enumerable.Range(0, BulkEoas).Select(i => "0xe" + i.ToString("x").PadLeft(39, '0')).ToArray();

        private static async Task<Workload> BuildWorkloadAsync(
            bool snap2, WhaleShape whale = WhaleShape.Churned, bool selfDestructLargeContract = false)
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(
                generatedAccounts: ActorAccounts, extraPrefunded: BulkEoaAddresses(), hardfork: Hardfork(snap2));
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

            for (var i = 0; i < ChurnBlocks; i++)
            {
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

            return new Workload(sequencer, whaleAddress, largeStorageAddress);
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
                        EnableFlatReconcile = true,
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
            Assert.True(line != null, "no 'snap.flat.reconcile done' line was logged — EnableFlatReconcile did not run");
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
            var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
            var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, live.Server.Bundle.TrieNodes);
            var accountDecoder = new AccountEncoder();
            foreach (var account in live.Workload.Sequencer.Accounts.All.Take(TouchedAccounts))
            {
                var synced = await recovered.GetAccountAsync(account.Address);
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
