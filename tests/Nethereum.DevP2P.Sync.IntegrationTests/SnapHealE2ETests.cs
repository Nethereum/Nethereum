using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Chain.TestData;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class SnapHealE2ETests
    {
        private readonly ITestOutputHelper _output;

        public SnapHealE2ETests(ITestOutputHelper output) => _output = output;

        [Fact(Skip = "Heal-by-racing-the-pivot still not reproduced; four measured attempts, each ruling " +
                     "something out. (1) 10 blocks of churn: no effect - PivotStaleDistanceBlocks was 120, " +
                     "so the pivot never went stale. (2) 4k then 20k accounts + 2KB snap pages + 139 blocks " +
                     "sealed during the run: pivot still never refreshed, because RootRefreshIntervalMs=12s " +
                     "polls less often than Phase 2 takes. (3) threshold 5 + 150ms refresh + continuous " +
                     "sealing: the refresher fires but snap never converges - it chases a moving target for " +
                     "the full deadline, which is exactly what the 120 default exists to prevent. " +
                     "(4) threshold 5 + bounded churn (30 blocks) then stop: still does not converge. " +
                     "So the threshold is now configurable (that part works and is used here), but a " +
                     "settings-only race is not a reliable way to reach heal. Superseded by deterministic " +
                     "fault injection, which DOES prove heal: see " +
                     "Given_TheSnapDownloadCompletesCleanly_When_SyncedTrieNodesAreRemovedBeforeHeal_Then_HealRefetchesExactlyThemAndTheRootStillMatches " +
                     "below, which drives TrieHealer directly (the same class and construction " +
                     "SnapBootstrapper.CreateHealer uses) against the joiner's own store and a live peer " +
                     "connection, after deleting known trie-node blobs by hash — divergence constructed, " +
                     "not raced for. That deterministic test also found a SECOND, independent reason this " +
                     "race could never have worked even if attempts 1-4 had landed it: both nodes here use " +
                     "CreateWithRocksDb's default advertiseSnap2=true, which negotiates snap/2, under which " +
                     "Snap1Handler's GetTrieNodes case is a silent no-op (breaks without sending ANY " +
                     "response) — TrieHealer's GetTrieNodes calls would have hung against the timeout " +
                     "forever, never actually completing heal, even had the pivot race succeeded. See " +
                     "Snap1Handler.cs and the class remarks below for the full finding. This racing test is " +
                     "kept, still skipped, as the record of why the settings-only approach was abandoned; " +
                     "nobody should have to re-run attempts 1-4.")]
        public async Task Given_TheSequencerKeepsSealingDuringSnap_When_ThePivotMovesUnderTheJoiner_Then_HealRunsAndTheStateStillMatches()
        {
            var producerNode = DevChainNode.CreateWithRocksDb(snapSoftResponseLimit: 2 * 1024);
            var joiner = DevChainNode.CreateWithRocksDb();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                var load = await DevChainLoad.GenerateAsync(
                    producerNode, producer, blocks: 80, contracts: 4, extraAccounts: 20000);
                _output.WriteLine($"load: height={load.Height} txs={load.Transactions} contracts={load.ContractAddresses.Count} accounts={load.ExtraAccounts}");

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var follower = await DevChainFollower.AttachAsync(joiner);
                var log = new CapturingLogger(_output);
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                    using var keepSealing = new CancellationTokenSource();

                    var bootstrap = follower.SnapBootstrapAsync(deadline.Token, log, pivotStaleDistanceBlocks: 5, rootRefreshIntervalMs: 150);
                    var churn = KeepSealingAsync(producerNode, producer, blocks: 30, keepSealing.Token);

                    var sealedDuring = await churn;
                    var result = await bootstrap;
                    keepSealing.Cancel();

                    _output.WriteLine($"sealed {sealedDuring} block(s) while snap was running");
                    Assert.True(result.Ran, $"snap did not run: {result.SkipReason}");
                    _output.WriteLine(
                        $"snap pivot={result.PivotBlockNumber} accounts={result.AccountCount} " +
                        $"slots={result.SlotCount} bytecode={result.BytecodeCount}");

                    var healEntered = log.Contains("to=Complete") && log.Contains("from=Phase3");
                    var reconcilePatched = log.Lines.Any(l =>
                        l.Contains("snap.flat.reconcile done") && !l.Contains("patched=0/0"));

                    _output.WriteLine($"healEntered={healEntered} reconcilePatched={reconcilePatched}");
                    foreach (var line in log.Lines.Where(l => l.Contains("phase.transition") || l.Contains("reconcile done")))
                        _output.WriteLine($"  {line}");

                    Assert.True(healEntered || reconcilePatched,
                        "the pivot moved under the joiner but neither heal nor a reconcile patch ran — " +
                        "the heal path is still unexercised");

                    Assert.Null(joiner.Bundle.Metadata.GetSnapSyncState());

                    var pivot = (long)result.PivotBlockNumber;
                    var expectedRoot = (await producerNode.Bundle.Blocks.GetByNumberAsync(pivot))?.StateRoot;
                    Assert.True(ByteUtil.AreEqual(result.PivotStateRoot, expectedRoot),
                        "pivot state root does not match the producer's after heal");

                    foreach (var address in load.ContractAddresses)
                    {
                        var key = address.ToLowerInvariant();
                        var mine = await joiner.Bundle.State.GetAccountAsync(key);
                        Assert.NotNull(mine);

                        var myCode = await joiner.Bundle.State.GetCodeAsync(mine.CodeHash);
                        Assert.True(myCode != null && myCode.Length > 0, $"no code synced for {address}");

                        for (var slot = 0; slot < 3; slot++)
                        {
                            var mySlot = await joiner.Bundle.State.GetStorageAsync(key, slot);
                            var theirSlot = await producerNode.Bundle.State.GetStorageAsync(key, slot);
                            Assert.True(ByteUtil.AreEqual(mySlot, theirSlot),
                                $"storage slot {slot} differs for {address} after heal");
                        }
                    }
                }
                finally
                {
                    await follower.DisposeAsync();
                }
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_TheSnapDownloadCompletesCleanly_When_SyncedTrieNodesAreRemovedBeforeHeal_Then_HealRefetchesExactlyThemAndTheRootStillMatches()
        {
            var producerNode = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            var joiner = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                var load = await DevChainLoad.GenerateAsync(
                    producerNode, producer, blocks: 80, contracts: 4, extraAccounts: 2000);
                _output.WriteLine(
                    $"load: height={load.Height} txs={load.Transactions} contracts={load.ContractAddresses.Count} accounts={load.ExtraAccounts}");

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var follower = await DevChainFollower.AttachAsync(joiner);
                try
                {
                    using var downloadDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));

                    var result = await follower.SnapBootstrapAsync(downloadDeadline.Token, new CapturingLogger(_output));
                    Assert.True(result.Ran, $"snap bootstrap did not run: {result.SkipReason}");
                    _output.WriteLine(
                        $"snap pivot={result.PivotBlockNumber} accounts={result.AccountCount} " +
                        $"slots={result.SlotCount} bytecode={result.BytecodeCount}");

                    await result.HistoryBackfill;

                    var pivotRoot = result.PivotStateRoot;
                    var expectedRoot = (await producerNode.Bundle.Blocks.GetByNumberAsync((long)result.PivotBlockNumber))?.StateRoot;
                    Assert.True(ByteUtil.AreEqual(pivotRoot, expectedRoot),
                        "download did not converge on the producer's root — heal cannot be meaningfully " +
                        "exercised from a baseline that is already broken");

                    var bundle = joiner.Bundle;
                    var trieNodes = (INodeBlobStore)bundle.TrieNodes;

                    var rootBlob = trieNodes.Get(pivotRoot);
                    Assert.True(rootBlob != null, "the downloaded root node is not in the joiner's store");

                    var decoder = new NodeDecoder();
                    var rootNode = decoder.DecodeFromRlpData(
                        rootBlob, null, Array.Empty<byte>(), decodeHashNodes: false, bundle.StateTrieNodes);
                    var branch = Assert.IsType<BranchNode>(rootNode);

                    var targets = branch.Children
                        .OfType<HashNode>()
                        .Where(h => h.Hash is { Length: 32 })
                        .Take(2)
                        .Select(h => h.Hash)
                        .ToList();
                    Assert.True(targets.Count == 2,
                        $"account trie root did not branch into at least two real subtree references " +
                        $"(found {targets.Count}) — increase extraAccounts");

                    var originalBlobs = new List<byte[]>();
                    foreach (var hash in targets)
                    {
                        var blob = trieNodes.Get(hash);
                        Assert.True(blob != null, $"target node 0x{hash.ToHex()} is missing before the test even removes anything");
                        originalBlobs.Add(blob);
                    }

                    var scheduler = new FetchRequestScheduler(
                        joiner.DialPool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
                    IHealNodeSink BuildSink() =>
                        (bundle as IHealNodeSinkProvider)?.CreateHealSink() ?? new HashHealNodeSink(trieNodes);
                    TrieHealer BuildHealer() => new TrieHealer(
                        scheduler, BuildSink(), bundle.StateTrieNodes,
                        logger: new CapturingLogger(_output),
                        flatWriter: bundle.State as ISnapFlatStateWriter,
                        codeStore: bundle.State);

                    using var healDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                    var quiet = await BuildHealer().HealAsync(pivotRoot, pivotBlock: result.PivotBlockNumber, ct: healDeadline.Token);
                    _output.WriteLine($"heal (intact store): matched={quiet.Matched} fetched={quiet.TotalNodesFetched}");
                    Assert.True(quiet.Matched, "heal over an already-intact store must still report matched");
                    Assert.Equal(0, quiet.TotalNodesFetched);

                    foreach (var hash in targets) trieNodes.Delete(hash);
                    foreach (var hash in targets)
                        Assert.True(trieNodes.Get(hash) == null, $"node 0x{hash.ToHex()} was not actually removed");

                    var healed = await BuildHealer().HealAsync(pivotRoot, pivotBlock: result.PivotBlockNumber, ct: healDeadline.Token);
                    _output.WriteLine($"heal (2 nodes removed): matched={healed.Matched} fetched={healed.TotalNodesFetched}");

                    Assert.True(healed.Matched, "heal did not converge back onto the pivot root");
                    Assert.Equal(targets.Count, healed.TotalNodesFetched);

                    for (var i = 0; i < targets.Count; i++)
                    {
                        var restored = trieNodes.Get(targets[i]);
                        Assert.True(restored != null, $"node 0x{targets[i].ToHex()} was not restored");
                        Assert.True(ByteUtil.AreEqual(restored, originalBlobs[i]),
                            $"restored node 0x{targets[i].ToHex()} does not byte-match what was removed");
                    }
                }
                finally
                {
                    await follower.DisposeAsync();
                }
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        private static async Task<int> KeepSealingAsync(
            DevChainNode node, DevChainProducer producer, int blocks, CancellationToken ct)
        {
            var sender = ChainAccounts.Operator;
            var nonce = BigInteger.Zero;
            var sealedCount = 0;

            for (var i = 0; i < blocks && !ct.IsCancellationRequested; i++)
            {
                try
                {
                    var tx = DevChainTransactions.SignEip1559(
                        sender.PrivateKey, (int)node.ChainId,
                        ChainAccounts.Recipient.Address, nonce, value: 111 + i);
                    await node.TxPool.AddAsync(tx);
                    nonce += 1;

                    await producer.ProduceAsync();
                    sealedCount++;
                    await Task.Delay(40, ct);
                }
                catch (OperationCanceledException) { break; }
            }

            return sealedCount;
        }

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ITestOutputHelper _out;
            private readonly List<string> _lines = new List<string>();
            private readonly object _gate = new object();

            public CapturingLogger(ITestOutputHelper output) => _out = output;

            public IReadOnlyList<string> Lines
            {
                get { lock (_gate) return _lines.ToList(); }
            }

            public bool Contains(string fragment) => Lines.Any(l => l.Contains(fragment));

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var line = formatter(state, exception);
                lock (_gate) _lines.Add(line);
            }
        }
    }
}
