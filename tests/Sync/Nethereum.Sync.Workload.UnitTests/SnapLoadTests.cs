using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.CoreChain.State;
using Nethereum.Model;
using Nethereum.Chain.TestData;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.Documentation;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Trait("Category", "Load")]
    public class SnapLoadTests
    {
        private const int Eoas = 50_000;
        private const int Contracts = 500;
        private readonly ITestOutputHelper _out;
        public SnapLoadTests(ITestOutputHelper @out) => _out = @out;

        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);
        private static BigInteger Num(byte[] b) => b == null || b.Length == 0 ? BigInteger.Zero : new BigInteger(b, isUnsigned: true, isBigEndian: true);

        private static async Task<bool> WaitUntilAsync(Func<bool> c, TimeSpan t)
        {
            var d = DateTime.UtcNow + t;
            while (DateTime.UtcNow < d) { if (c()) return true; await Task.Delay(100); }
            return c();
        }

        private sealed class TrustedTip : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _tip;
            public TrustedTip(ulong n, byte[] h, byte[] r) => _tip = new CanonicalTip { BlockNumber = n, BlockHash = h, StateRoot = r };
            public string Name => "LoadTrustedTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(_tip);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong n, CancellationToken ct)
                => Task.FromResult(n == _tip.BlockNumber ? (_tip.StateRoot, _tip.BlockHash) : ((byte[])null, (byte[])null));
        }

        private async Task<(InProcessSequencerDriver sequencer, List<string> incrementContracts)> BuildLargeStateAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var eoaAddresses = Enumerable.Range(0, Eoas).Select(i => "0xf" + i.ToString("x").PadLeft(39, '0')).ToArray();
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 200, extraPrefunded: eoaAddresses);
            _out.WriteLine($"genesis: {Eoas} EOAs prefunded in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            await new WorkloadV1().BuildAsync(sequencer);
            _out.WriteLine($"WorkloadV1: {await sequencer.Blocks.GetHeightAsync()} blocks in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var roster = sequencer.Accounts.All;
            var incrementContracts = new List<string>(Contracts);
            for (int i = 0; i < Contracts; i++)
            {
                incrementContracts.Add(sequencer.QueueDeploy(roster[i % roster.Count], WorkloadContracts.IncrementOnCallBytecode));
                if ((i + 1) % 20 == 0) await sequencer.ProduceBlockAsync();
            }
            await sequencer.ProduceBlockAsync();

            sequencer.QueueDeploy(roster[0], WorkloadContracts.LargeStorageBytecode, gasLimit: 29_000_000);
            await sequencer.ProduceBlockAsync();
            while ((await sequencer.Blocks.GetHeightAsync()) <= 80)
            {
                for (int i = 0; i < 8; i++)
                    sequencer.QueueTransfer(roster[i % roster.Count], roster[(i + 1) % roster.Count].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }
            await ProduceEmptyTailAsync(sequencer);
            _out.WriteLine($"produced {await sequencer.Blocks.GetHeightAsync()} blocks ({Contracts} increment contracts) in {sw.ElapsedMilliseconds} ms");
            return (sequencer, incrementContracts);
        }

        /// <summary>A no-transaction tail, so the blocks above the pivot carry no bodies the bootstrap
        /// must fetch. It does NOT freeze state: under Prague the EIP-2935 and EIP-4788 system calls write
        /// a slot on every block, transactions or not, so a follower still has to execute the trail to
        /// reach the tip — which is what <see cref="CatchUpToTipAsync"/> does before any state is compared.</summary>
        internal static async Task ProduceEmptyTailAsync(InProcessSequencerDriver sequencer, int blocks = 40)
        {
            for (int i = 0; i < blocks; i++) await sequencer.ProduceBlockAsync();
        }

        [Fact]
        public async Task Load_LargeContract_WhalePath_AllSlotsEquivalent()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            var roster = sequencer.Accounts.All;
            var largeAddr = sequencer.QueueDeploy(roster[0], WorkloadContracts.LargeStorageBytecode, gasLimit: 29_000_000);
            await sequencer.ProduceBlockAsync();
            while ((await sequencer.Blocks.GetHeightAsync()) <= 70)
            {
                for (int i = 0; i < 4; i++)
                    sequencer.QueueTransfer(roster[i % roster.Count], roster[(i + 1) % roster.Count].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }
            await ProduceEmptyTailAsync(sequencer);

            var expectedStorage = await sequencer.State.GetAllStorageAsync(largeAddr);
            Assert.True(expectedStorage.Count >= 1000, $"expected a large contract; got {expectedStorage.Count} slots");
            _out.WriteLine($"large contract deployed with {expectedStorage.Count} storage slots");

            await using var server = await WireServerNode.StartAsync(sequencer, snapResponseLimit: 8 * 1024);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(30)), "no snap peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var canonical = new TrustedTip(head, headHash, headHeader.StateRoot);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap-whale-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                await CatchUpToTipAsync(bundle, sequencer);

                var serverAccounts = await sequencer.State.GetAllAccountsAsync();
                foreach (var kv in serverAccounts)
                {
                    var got = await bundle.State.GetAccountAsync(kv.Key);
                    Assert.True(got != null, $"account {kv.Key} missing from follower");
                    Assert.Equal(kv.Value.Balance, got.Balance);
                    Assert.Equal(kv.Value.Nonce, got.Nonce);
                    var expStore = await sequencer.State.GetAllStorageAsync(kv.Key);
                    if (expStore.Count == 0) continue;
                    var gotStore = await bundle.State.GetAllStorageAsync(kv.Key);
                    foreach (var slot in expStore)
                    {
                        Assert.True(gotStore.TryGetValue(slot.Key, out var v), $"slot missing for {kv.Key}");
                        Assert.Equal(Num(slot.Value), Num(v));
                    }
                }
                var whale = await bundle.State.GetAllStorageAsync(largeAddr);
                Assert.Equal(expectedStorage.Count, whale.Count);
                _out.WriteLine($"whale contract: {whale.Count} slots snapped + verified");
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        private static async Task<InProcessSequencerDriver> TailedWorkloadAsync()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            await new WorkloadV1().BuildAsync(sequencer);
            await ProduceEmptyTailAsync(sequencer);
            return sequencer;
        }

        [Fact]
        public async Task Given_ASnapBootstrappedFollower_When_ItGoesLiveAtTheTrailedPivot_Then_ItsExecutedHeadIsThePivotAndNotTheTip()
        {
            var sequencer = await TailedWorkloadAsync();
            await WithSnappedFollowerAsync(sequencer, bulkSync: false, TimeSpan.FromMinutes(3),
                async (follower, result, tip) =>
                {
                    var committedHead = follower.Metadata.GetLastBlock();
                    Assert.Equal(result.PivotBlockNumber, committedHead);
                    Assert.Equal((BigInteger)tip, await follower.Blocks.GetHeightAsync());
                    Assert.True(committedHead < tip,
                        $"the pivot must trail the tip for this to be a handoff at all: committed {committedHead}, tip {tip}");
                    _out.WriteLine($"go-live: committed head {committedHead}, archive height {tip}");
                });
        }

        [Fact]
        public async Task Given_ASnapBootstrappedFollower_When_ItForwardExecutesFromItsCommittedHead_Then_ItsStateEqualsTheSequencerAtTheTip()
        {
            var sequencer = await TailedWorkloadAsync();
            await WithSnappedFollowerAsync(sequencer, bulkSync: false, TimeSpan.FromMinutes(3),
                async (follower, result, tip) =>
                {
                    var history = Eip2935Constants.HistoryStorageAddress;
                    var beforeCatchUp = await follower.State.GetAllStorageAsync(history);
                    var expected = await sequencer.State.GetAllStorageAsync(history);
                    Assert.True(beforeCatchUp.Count < expected.Count,
                        "the trail must be unexecuted before catch-up, or this test cannot observe the handoff");

                    var executed = await CatchUpToTipAsync(follower, sequencer);
                    Assert.Equal((int)(tip - result.PivotBlockNumber), executed);
                    Assert.Equal((BigInteger)tip, (BigInteger)follower.Metadata.GetLastBlock());

                    var got = await follower.State.GetAllStorageAsync(history);
                    Assert.Equal(expected.Count, got.Count);
                    foreach (var slot in expected)
                    {
                        Assert.True(got.TryGetValue(slot.Key, out var v),
                            $"block-hash history slot 0x{slot.Key.ToHex()} still missing after catching up to the tip");
                        Assert.Equal(Num(slot.Value), Num(v));
                    }
                    _out.WriteLine($"handoff: executed {executed} trail blocks, {got.Count} history slots match at tip {tip}");
                });
        }

        private async Task AssertFullEquivalenceAsync(InProcessSequencerDriver reference, IChainStoreBundle follower)
        {
            await CatchUpToTipAsync(follower, reference);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Nethereum.Chain.TestData.ChainEquivalence.AssertStateEquivalentAsync(
                reference.Stores, Nethereum.Chain.TestData.ChainStores.From(follower));
            _out.WriteLine($"state equivalence OK in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var head = (ulong)await reference.Blocks.GetHeightAsync();
            int headers = 0, txCount = 0, receiptCount = 0, failedTxs = 0, logCount = 0;
            var allFollowerLogs = new List<FilteredLog>();
            for (ulong n = 0; n <= head; n++)
            {
                var bn = new BigInteger(n);
                var refHash = await reference.Blocks.GetHashByNumberAsync(bn);
                Assert.True(refHash != null && Nethereum.Util.ByteUtil.AreEqual(refHash, await follower.Blocks.GetHashByNumberAsync(bn)),
                    $"block {n} header hash mismatch");
                headers++;

                var refTxHashes = await reference.Transactions.GetHashesByBlockHashAsync(refHash);
                var gotTxHashes = await follower.Transactions.GetHashesByBlockHashAsync(refHash);
                Assert.Equal(refTxHashes.Count, gotTxHashes.Count);
                var refRec = await reference.Receipts.GetByBlockNumberAsync(bn);
                var gotRec = await follower.Receipts.GetByBlockNumberAsync(bn);
                Assert.Equal(refRec.Count, gotRec.Count);

                for (int i = 0; i < refTxHashes.Count; i++)
                {
                    var txHash = refTxHashes[i];
                    Assert.True(Nethereum.Util.ByteUtil.AreEqual(txHash, gotTxHashes[i]), $"block {n} tx {i} hash mismatch");

                    Assert.Equal(refRec[i].HasSucceeded, gotRec[i].HasSucceeded);
                    if (refRec[i].HasSucceeded == false) failedTxs++;

                    Assert.True(await follower.Transactions.GetByHashAsync(txHash) != null, $"tx {txHash.ToHex()} not found by hash");
                    var recByHash = await follower.Receipts.GetByTxHashAsync(txHash);
                    Assert.True(recByHash != null, $"receipt for tx {txHash.ToHex()} not found by hash");
                    Assert.Equal(refRec[i].HasSucceeded, recByHash.HasSucceeded);

                    Assert.Equal(refRec[i].Logs?.Count ?? 0, (await follower.Logs.GetLogsByTxHashAsync(txHash)).Count);
                }
                txCount += refTxHashes.Count;
                receiptCount += refRec.Count;

                var expectedBlockLogs = refRec.Sum(r => r.Logs?.Count ?? 0);
                var blockLogs = await follower.Logs.GetLogsByBlockNumberAsync(bn);
                Assert.Equal(expectedBlockLogs, blockLogs.Count);
                allFollowerLogs.AddRange(blockLogs);
                logCount += expectedBlockLogs;
            }
            Assert.True(failedTxs > 0, "expected at least one failed (status-0) tx in the workload archive");
            _out.WriteLine($"archive equivalence OK: {headers} headers, {txCount} txs ({failedTxs} failed), {receiptCount} receipts, {logCount} logs in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            Assert.True(allFollowerLogs.Count > 0, "workload should have emitted logs to filter on");

            var logAddr = allFollowerLogs[0].Address;
            var byAddr = await follower.Logs.GetLogsAsync(new LogFilter { Addresses = new List<string> { logAddr } });
            Assert.True(byAddr.Count > 0, "address filter returned nothing");
            Assert.Equal(allFollowerLogs.Count(l => string.Equals(l.Address, logAddr, StringComparison.OrdinalIgnoreCase)), byAddr.Count);

            var topicLog = allFollowerLogs.FirstOrDefault(l => l.Topics != null && l.Topics.Count > 0);
            Assert.True(topicLog != null, "workload should have emitted a topic'd log (MultiStorageLogger)");
            var topic0 = topicLog.Topics[0];
            var byTopic = await follower.Logs.GetLogsAsync(new LogFilter { Topics = new List<List<byte[]>> { new List<byte[]> { topic0 } } });
            Assert.True(byTopic.Count > 0, "topic filter returned nothing");
            Assert.Equal(
                allFollowerLogs.Count(l => l.Topics != null && l.Topics.Count > 0 && Nethereum.Util.ByteUtil.AreEqual(l.Topics[0], topic0)),
                byTopic.Count);

            var lo = new BigInteger(head / 4);
            var hi = new BigInteger(head / 2 + 1);
            var byRange = await follower.Logs.GetLogsAsync(new LogFilter { FromBlock = lo, ToBlock = hi });
            Assert.Equal(allFollowerLogs.Count(l => l.BlockNumber >= lo && l.BlockNumber <= hi), byRange.Count);

            var combo = await follower.Logs.GetLogsAsync(new LogFilter
            {
                Addresses = new List<string> { topicLog.Address },
                Topics = new List<List<byte[]>> { new List<byte[]> { topic0 } }
            });
            Assert.Equal(
                allFollowerLogs.Count(l => string.Equals(l.Address, topicLog.Address, StringComparison.OrdinalIgnoreCase)
                    && l.Topics != null && l.Topics.Count > 0 && Nethereum.Util.ByteUtil.AreEqual(l.Topics[0], topic0)),
                combo.Count);
            _out.WriteLine($"log filters OK: by-address {byAddr.Count}, by-topic {byTopic.Count}, range[{lo}..{hi}] {byRange.Count}, addr+topic {combo.Count} in {sw.ElapsedMilliseconds} ms");
        }

        private static readonly byte[] KeccakEmpty =
            "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470".HexToByteArray();

        [Fact]
        public async Task Snap_TransmitsWithdrawals_ToFollower()
        {
            // EIP-4895 withdrawals are a configurable core feature. Produce a chain that carries them on a few
            // blocks, snap a follower over real RLPx, and assert every block's withdrawals round-trip exactly.
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 30);
            var roster = sequencer.Accounts.All;
            for (int b = 1; b <= 70; b++)
            {
                sequencer.QueueTransfer(roster[b % roster.Count], roster[(b + 1) % roster.Count].Address, Eth(1));
                if (b == 5 || b == 30 || b == 60)
                {
                    sequencer.QueueWithdrawal("0x" + b.ToString("x").PadLeft(40, '0'), (ulong)(1000 * b));
                    sequencer.QueueWithdrawal("0x" + (b + 1).ToString("x").PadLeft(40, '0'), (ulong)(2000 * b));
                }
                await sequencer.ProduceBlockAsync();
            }
            await ProduceEmptyTailAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(30)), "no snap peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var canonical = new TrustedTip(head, headHash, headHeader.StateRoot);
            var dbPath = Path.Combine(Path.GetTempPath(), "snap-wd-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance, new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                int total = 0;
                for (ulong n = 0; n <= head; n++)
                {
                    var bn = new BigInteger(n);
                    var expected = await sequencer.Withdrawals.GetByBlockNumberAsync(bn) ?? new List<Withdrawal>();
                    var got = await bundle.Withdrawals.GetByBlockNumberAsync(bn) ?? new List<Withdrawal>();
                    Assert.Equal(expected.Count, got.Count);
                    for (int i = 0; i < expected.Count; i++)
                    {
                        Assert.Equal(expected[i].AmountInGwei, got[i].AmountInGwei);
                        Assert.True(Nethereum.Util.ByteUtil.AreEqual(expected[i].Address, got[i].Address), $"withdrawal address mismatch block {n}");
                    }
                    total += expected.Count;
                }
                Assert.Equal(6, total);
                _out.WriteLine($"snap transmitted {total} withdrawals across {head} blocks");
            }
            finally { try { Directory.Delete(dbPath, true); } catch { } }
        }

        private Task SnapAndAssertEquivalenceAsync(InProcessSequencerDriver sequencer, bool bulkSync, TimeSpan timeout)
            => WithSnappedFollowerAsync(sequencer, bulkSync, timeout,
                (follower, result, tip) => AssertFullEquivalenceAsync(sequencer, follower));

        private async Task WithSnappedFollowerAsync(
            InProcessSequencerDriver sequencer, bool bulkSync, TimeSpan timeout,
            Func<IChainStoreBundle, SnapBootstrapper.Result, ulong, Task> body)
        {
            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);
            var (pool, scheduler) = await ConnectAsync(server);
            var dbPath = Path.Combine(Path.GetTempPath(), "snap-bulk-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(
                    manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive, ownsManager: true, bulkSync: bulkSync);
                using var cts = new CancellationTokenSource(timeout);
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapSyncOrchestrator.RunAsync(bundle, pool, scheduler,
                    new TrustedTip(head, headHash, headHeader.StateRoot), activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                bundle.FinishBulkSync();
                _out.WriteLine($"snap ({(bulkSync ? "BULK" : "normal")}): {result.AccountCount} accounts in {result.SlotCount} slots");
                await body(bundle, result, head);
            }
            finally
            {
                await pool.DisposeAsync();
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task BulkSync_FullEquivalence_WithLogs()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            var roster = sequencer.Accounts.All;
            await new WorkloadV1().BuildAsync(sequencer);
            while ((await sequencer.Blocks.GetHeightAsync()) <= 70)
            {
                for (int i = 0; i < 4; i++)
                    sequencer.QueueTransfer(roster[i % roster.Count], roster[(i + 1) % roster.Count].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }
            await ProduceEmptyTailAsync(sequencer);
            await SnapAndAssertEquivalenceAsync(sequencer, bulkSync: true, TimeSpan.FromMinutes(3));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-sync", "Snap-bootstrap a follower and verify full state equivalence")]
        public async Task Load_FullStateEquivalence_50k()
        {
            var (sequencer, incrementContracts) = await BuildLargeStateAsync();
            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(30)), "no snap peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var canonical = new TrustedTip(head, headHash, headHeader.StateRoot);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap-load-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                _out.WriteLine($"snap completed: {result.AccountCount} accounts, {result.SlotCount} slots, {result.BytecodeCount} codes in {sw.ElapsedMilliseconds} ms");

                await AssertFullEquivalenceAsync(sequencer, bundle);
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        internal static async Task<(PeerPoolManager pool, FetchRequestScheduler scheduler)> ConnectAsync(WireServerNode server)
        {
            var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(30)), "no snap peer");
            return (pool, new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions()));
        }

        internal static async Task<int> ForwardExecuteAsync(IChainStoreBundle bundle, InProcessSequencerDriver sequencer, int fromBlock)
        {
            var chainConfig = new Nethereum.CoreChain.ChainConfig { ChainId = sequencer.ChainId, BaseFee = BigInteger.Zero, Coinbase = sequencer.SequencerAddress, Hardfork = sequencer.Hardfork };
            var activations = new FixedChainActivations(HardforkNames.Parse(sequencer.Hardfork));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new Nethereum.CoreChain.IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new Nethereum.CoreChain.BlockExecutor(
                bundle.State, bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc, rewardPolicy: Nethereum.CoreChain.NoRewardPolicy.Instance, trieNodeStore: bundle.TrieNodes);
            var importer = new Nethereum.CoreChain.BlockImporter(engine, bundle.Blocks, bundle.State,
                bundle.Transactions, bundle.Receipts, bundle.Logs, uncleStore: bundle.Uncles,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: bundle.BlockAccessLists);
            int executed = 0;
            foreach (var (header, txs) in sequencer.ProducedBlockData.Skip(fromBlock))
            {
                var result = await importer.ImportAsync(header, txs, null, null);
                Assert.True(result.RootMatches, $"forward block {header.BlockNumber} diverged");
                bundle.Metadata.Commit((ulong)header.BlockNumber, result.BlockHash);
                executed++;
            }
            return executed;
        }

        internal static Task<int> CatchUpToTipAsync(IChainStoreBundle follower, InProcessSequencerDriver sequencer)
            => ForwardExecuteAsync(follower, sequencer, (int)follower.Metadata.GetLastBlock());

        [Fact]
        public async Task Load_ForwardExecutionStress_50k()
        {
            var (sequencer, incrementContracts) = await BuildLargeStateAsync();
            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            var (pool, scheduler) = await ConnectAsync(server);
            var dbPath = Path.Combine(Path.GetTempPath(), "snap-fwd-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapSyncOrchestrator.RunAsync(bundle, pool, scheduler, new TrustedTip(head, headHash, headHeader.StateRoot),
                    activations, NullLogger.Instance, new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                var pivotHead = (int)bundle.Metadata.GetLastBlock();
                var trail = await CatchUpToTipAsync(bundle, sequencer);
                Assert.Equal((int)head - pivotHead, trail);

                var roster = sequencer.Accounts.All;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                const int fwdBlocks = 60;
                for (int b = 0; b < fwdBlocks; b++)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        var recipient = "0xf" + (b * 30 + i).ToString("x").PadLeft(39, '0');
                        sequencer.QueueTransfer(roster[i % roster.Count], recipient, Eth(1));
                    }
                    // MODIFY existing contract storage at scale: each call SLOADs the snapped slot 0 and
                    // SSTOREs slot0+1 (ERC20-balance shape) — exercising read-modify-write of snapped state.
                    // Callers are DISJOINT from the transfer senders (above) so no sender has two txs in a
                    // block — the producer would group same-sender txs, reordering vs ProducedBlockData.
                    for (int c = 0; c < 10; c++)
                        sequencer.QueueCall(roster[(100 + c) % roster.Count], incrementContracts[(b * 10 + c) % incrementContracts.Count], Array.Empty<byte>());
                    await sequencer.ProduceBlockAsync();
                }
                _out.WriteLine($"produced {fwdBlocks} forward blocks in {sw.ElapsedMilliseconds} ms");

                sw.Restart();
                var executed = await CatchUpToTipAsync(bundle, sequencer);
                Assert.Equal(fwdBlocks, executed);
                _out.WriteLine($"forward-executed {executed} blocks on snapped state in {sw.ElapsedMilliseconds} ms");

                Assert.Equal((BigInteger)(pivotHead + 1), (await bundle.Diffs.GetOldestDiffBlockAsync()).Value);
                Assert.Equal((BigInteger)((int)head + fwdBlocks), (await bundle.Diffs.GetNewestDiffBlockAsync()).Value);
                await AssertFullEquivalenceAsync(sequencer, bundle);
            }
            finally
            {
                await pool.DisposeAsync();
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        private static async Task<CanonicalTip> TipAt(WireServerNode server, ulong block)
        {
            var h = await server.Bundle.Blocks.GetByNumberAsync(block);
            var hash = await server.Bundle.Blocks.GetHashByNumberAsync(block);
            return new CanonicalTip { BlockNumber = block, BlockHash = hash, StateRoot = h.StateRoot };
        }

        private sealed class GatedTip : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _initial; private readonly CanonicalTip _head; private readonly Func<bool> _advanced;
            public GatedTip(CanonicalTip initial, CanonicalTip head, Func<bool> advanced)
            { _initial = initial; _head = head; _advanced = advanced; }
            public string Name => "GatedTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(_advanced() ? _head : _initial);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong n, CancellationToken ct)
                => Task.FromResult(((byte[])null, (byte[])null));
        }

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _m = new();
            public System.Collections.Generic.IReadOnlyCollection<string> Messages => _m;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel l) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel l, Microsoft.Extensions.Logging.EventId e,
                TState s, Exception ex, Func<TState, Exception, string> f) => _m.Enqueue(f(s, ex) + (ex != null ? " " + ex.Message : ""));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-sync", "Rolling pivot and heal phase reconcile to the moving head")]
        public async Task Load_RollingPivotHealAtScale_50k()
        {
            var (sequencer, incrementContracts) = await BuildLargeStateAsync();
            var baseHead = (ulong)await sequencer.Blocks.GetHeightAsync();
            var roster = sequencer.Accounts.All;
            for (int b = 0; b < 60; b++)
            {
                for (int i = 0; i < 30; i++)
                    sequencer.QueueTransfer(roster[i % roster.Count], "0xf" + (b * 30 + i + 2000).ToString("x").PadLeft(39, '0'), Eth(1));
                await sequencer.ProduceBlockAsync();
            }
            await ProduceEmptyTailAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer, snapResponseLimit: 8 * 1024);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var tipAdvanced = 0;
            var source = new GatedTip(
                await TipAt(server, baseHead), await TipAt(server, head),
                () => Volatile.Read(ref tipAdvanced) == 1);

            var (pool, scheduler) = await ConnectAsync(server);
            var dbPath = Path.Combine(Path.GetTempPath(), "snap-heal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var log = new CapturingLogger();

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var runTask = SnapSyncOrchestrator.RunAsync(bundle, pool, scheduler, source, activations, log,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true, RootRefreshIntervalMs = 50 }, cts.Token);

                Assert.True(await WaitUntilAsync(
                        () => log.Messages.Any(m => m.Contains("snap.verify", StringComparison.OrdinalIgnoreCase)),
                        TimeSpan.FromMinutes(2)),
                    "phase 2 never started streaming");
                Volatile.Write(ref tipAdvanced, 1);

                var result = await runTask;
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                Assert.True(result.PivotBlockNumber > baseHead, $"pivot never rotated (still {result.PivotBlockNumber})");
                _out.WriteLine($"heal snap: {result.AccountCount} accounts, pivot {result.PivotBlockNumber} in {sw.ElapsedMilliseconds} ms");

                Assert.Contains(log.Messages, m => m.Contains("entering heal phase", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(log.Messages, m => m.Contains("Heal complete", StringComparison.OrdinalIgnoreCase) && m.Contains("matched=True"));

                await AssertFullEquivalenceAsync(sequencer, bundle);
            }
            finally
            {
                await pool.DisposeAsync();
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-sync", "A killed snap sync resumes from its checkpoint to a correct state")]
        public async Task Load_ResumeAfterKill_50k()
        {
            var (sequencer, incrementContracts) = await BuildLargeStateAsync();
            await using var server = await WireServerNode.StartAsync(sequencer, snapResponseLimit: 8 * 1024);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var canonical = new TrustedTip(head, await server.Bundle.Blocks.GetHashByNumberAsync(head),
                (await server.Bundle.Blocks.GetByNumberAsync(head)).StateRoot);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap-resume-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var (pool1, scheduler1) = await ConnectAsync(server);
                try
                {
                    using var kill = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    var r1 = await SnapSyncOrchestrator.RunAsync(bundle, pool1, scheduler1, canonical, activations,
                        NullLogger.Instance, new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, kill.Token);
                    _out.WriteLine($"killed first attempt: Ran={r1.Ran} reason={r1.SkipReason}");
                }
                catch (Exception ex) { _out.WriteLine($"first attempt interrupted mid-snap (expected): {ex.GetType().Name}"); }
                finally { await pool1.DisposeAsync(); }

                var (pool2, scheduler2) = await ConnectAsync(server);
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    var r2 = await SnapSyncOrchestrator.RunAsync(bundle, pool2, scheduler2, canonical, activations,
                        NullLogger.Instance, new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                    Assert.True(r2.Ran, r2.SkipReason);
                    _out.WriteLine($"resumed to completion: {r2.AccountCount} accounts");
                }
                finally { await pool2.DisposeAsync(); }

                await AssertFullEquivalenceAsync(sequencer, bundle);
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task Given_AChainOfTenThousandBlocks_When_AColdFollowerSnapSyncs_Then_ItStillMatchesAtThePivot()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 40);
            await new WorkloadV1().BuildAsync(sequencer);
            await ProduceEmptyTailAsync(sequencer, blocks: 9_950);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            var (pool, scheduler) = await ConnectAsync(server);
            var dbPath = Path.Combine(Path.GetTempPath(), "snap-depth10k-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, new TrustedTip(head, headHash, headHeader.StateRoot), activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                _out.WriteLine($"10k-depth snap completed in {sw.ElapsedMilliseconds} ms: pivot {result.PivotBlockNumber}, {result.AccountCount} accounts");

                var committedHead = bundle.Metadata.GetLastBlock();
                Assert.Equal(result.PivotBlockNumber, committedHead);
                Assert.True(committedHead < head, $"the pivot must trail the tip: committed {committedHead}, tip {head}");

                var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(result.PivotBlockNumber);
                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                var checkedCount = 0;
                foreach (var kv in await server.Bundle.State.GetAllAccountsAsync())
                {
                    var synced = await recovered.GetAccountAsync(kv.Key);
                    Assert.True(synced != null, $"account {kv.Key} missing from follower at pivot");
                    var expectedLeaf = serverAtPivot.Get(Sha3Keccack.Current.CalculateHash(kv.Key.HexToByteArray()));
                    Assert.True(expectedLeaf != null, $"account {kv.Key} missing from server trie at pivot");
                    var expected = accountDecoder.Decode(expectedLeaf);
                    Assert.Equal(expected.Balance, synced.Balance);
                    Assert.Equal(expected.Nonce, synced.Nonce);
                    checkedCount++;
                }
                _out.WriteLine($"pivot state verified for {checkedCount} accounts at block {result.PivotBlockNumber} (tip {head}) in {sw.ElapsedMilliseconds} ms total");
            }
            finally
            {
                await pool.DisposeAsync();
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
