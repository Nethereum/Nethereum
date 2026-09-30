using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Chain.TestData.Vectors;
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
    public class WireDepthSnapTests : IClassFixture<WireDepthSnapTests.Fixture>
    {
        private readonly Fixture _fx;
        private readonly ITestOutputHelper _out;

        public WireDepthSnapTests(Fixture fx, ITestOutputHelper @out)
        {
            _fx = fx;
            _out = @out;
        }

        [Fact]
        public void Given_AChainOfAThousandBlocks_When_AColdFollowerSnapSyncs_Then_ItsStateAtThePivotMatchesTheServer()
        {
            Assert.True(_fx.PivotAccountsChecked > 0, "no accounts were verified at the pivot");
            Assert.Equal(_fx.PivotAccountsChecked, _fx.PivotAccountsMatched);
            _out.WriteLine(
                $"pivot state check: {_fx.PivotAccountsMatched}/{_fx.PivotAccountsChecked} accounts matched the " +
                $"server's trie at pivot {_fx.Pivot} (chain height {_fx.Head}) in {_fx.SnapElapsedMs} ms");
        }

        [Fact]
        public void Given_AThousandBlockChain_When_TheFollowerGoesLive_Then_ItsCommittedHeadIsThePivotAndNotTheTip()
        {
            Assert.Equal(_fx.Pivot, _fx.CommittedHeadAtPivot);
            Assert.True(_fx.CommittedHeadAtPivot < _fx.Head,
                $"the pivot must trail the tip for this to be a handoff at all: committed {_fx.CommittedHeadAtPivot}, tip {_fx.Head}");
            _out.WriteLine($"go-live: committed head {_fx.CommittedHeadAtPivot} == pivot {_fx.Pivot}, tip {_fx.Head}");
        }

        [Fact]
        public async Task Given_AThousandBlockSnapSyncedFollower_When_ItForwardExecutesToTheTip_Then_EveryStoreMatchesTheSequencer()
        {
            Assert.Equal((int)(_fx.Head - _fx.Pivot), _fx.ForwardExecutedBlocks);
            Assert.Equal((BigInteger)(_fx.Pivot + 1), _fx.OldestDiffBlockAfterCatchup);
            Assert.Equal((BigInteger)_fx.Head, _fx.NewestDiffBlockAfterCatchup);
            Assert.Equal(_fx.Head, _fx.CommittedHeadAfterCatchup);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Nethereum.Chain.TestData.ChainEquivalence.AssertStateEquivalentAsync(
                _fx.Sequencer.Stores, Nethereum.Chain.TestData.ChainStores.From(_fx.Bundle));

            int txCount = 0, receiptCount = 0, logCount = 0;
            for (var n = 1L; n <= (long)_fx.Head; n++)
            {
                var hash = await _fx.Sequencer.Blocks.GetHashByNumberAsync(n);
                var expectedTxs = await _fx.Sequencer.Transactions.GetHashesByBlockHashAsync(hash);
                var gotTxs = await _fx.Bundle.Transactions.GetHashesByBlockHashAsync(hash);
                Assert.Equal(expectedTxs?.Count ?? 0, gotTxs?.Count ?? 0);

                var expectedReceipts = await _fx.Sequencer.Receipts.GetByBlockNumberAsync(n);
                var gotReceipts = await _fx.Bundle.Receipts.GetByBlockNumberAsync(n);
                Assert.Equal(expectedReceipts.Count, gotReceipts.Count);

                var expectedLogs = await _fx.Sequencer.Logs.GetLogsByBlockNumberAsync(n);
                var gotLogs = await _fx.Bundle.Logs.GetLogsByBlockNumberAsync(n);
                Assert.Equal(expectedLogs.Count, gotLogs.Count);

                txCount += expectedTxs?.Count ?? 0;
                receiptCount += expectedReceipts.Count;
                logCount += expectedLogs.Count;
            }

            _out.WriteLine(
                $"forward-executed {_fx.ForwardExecutedBlocks} trail blocks in {_fx.ForwardExecuteElapsedMs} ms; " +
                $"state + {txCount} txs, {receiptCount} receipts, {logCount} logs verified in {sw.ElapsedMilliseconds} ms");
        }

        public sealed class Fixture : IDisposable
        {
            private const int TailBlocks = 950;

            public InProcessSequencerDriver Sequencer { get; private set; }
            public IChainStoreBundle Bundle { get; private set; }
            public ulong Pivot { get; private set; }
            public ulong Head { get; private set; }
            public ulong CommittedHeadAtPivot { get; private set; }
            public int PivotAccountsChecked { get; private set; }
            public int PivotAccountsMatched { get; private set; }
            public int ForwardExecutedBlocks { get; private set; }
            public ulong CommittedHeadAfterCatchup { get; private set; }
            public BigInteger OldestDiffBlockAfterCatchup { get; private set; }
            public BigInteger NewestDiffBlockAfterCatchup { get; private set; }
            public long SnapElapsedMs { get; private set; }
            public long ForwardExecuteElapsedMs { get; private set; }

            private WireServerNode _server;
            private PeerPoolManager _pool;
            private string _dbPath;

            private sealed class TrustedTipSource : ICanonicalStateRootSource
            {
                private readonly CanonicalTip _tip;
                public TrustedTipSource(ulong number, byte[] hash, byte[] stateRoot)
                    => _tip = new CanonicalTip { BlockNumber = number, BlockHash = hash, StateRoot = stateRoot };
                public string Name => "DepthGateTrustedTip";
                public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(_tip);
                public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                    => Task.FromResult(blockNumber == _tip.BlockNumber ? (_tip.StateRoot, _tip.BlockHash) : ((byte[])null, (byte[])null));
            }

            private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (condition()) return true;
                    await Task.Delay(100);
                }
                return condition();
            }

            public Fixture()
            {
                InitializeAsync().GetAwaiter().GetResult();
            }

            private async Task InitializeAsync()
            {
                var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 40);
                await new WorkloadV1().BuildAsync(sequencer);
                await SnapLoadTests.ProduceEmptyTailAsync(sequencer, blocks: TailBlocks);

                var server = await WireServerNode.StartAsync(sequencer);
                var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
                var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
                var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

                var pool = new PeerPoolManager(
                    new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                    new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
                await pool.StartAsync(CancellationToken.None);
                pool.EnqueueCandidate(server.Enode);
                if (!await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(30)))
                    throw new Exception("no snap peer");

                var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
                var canonical = new TrustedTipSource(head, headHash, headHeader.StateRoot);

                var dbPath = Path.Combine(Path.GetTempPath(), "wire-depth-" + Guid.NewGuid().ToString("N"));
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var snapResult = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                if (!snapResult.Ran) throw new Exception($"snap did not run: {snapResult.SkipReason}");
                await snapResult.HistoryBackfill;
                SnapElapsedMs = sw.ElapsedMilliseconds;

                var pivot = snapResult.PivotBlockNumber;
                var committedHeadAtPivot = bundle.Metadata.GetLastBlock();

                var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(pivot);
                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                var checkedCount = 0;
                var matchedCount = 0;
                foreach (var kv in await server.Bundle.State.GetAllAccountsAsync())
                {
                    checkedCount++;
                    var synced = await recovered.GetAccountAsync(kv.Key);
                    if (synced == null) continue;
                    var expectedLeaf = serverAtPivot.Get(Sha3Keccack.Current.CalculateHash(kv.Key.HexToByteArray()));
                    if (expectedLeaf == null) continue;
                    var expected = accountDecoder.Decode(expectedLeaf);
                    if (expected.Balance == synced.Balance && expected.Nonce == synced.Nonce) matchedCount++;
                }

                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                var executed = await SnapLoadTests.CatchUpToTipAsync(bundle, sequencer);
                ForwardExecuteElapsedMs = sw2.ElapsedMilliseconds;

                Sequencer = sequencer;
                Bundle = bundle;
                _server = server;
                _pool = pool;
                _dbPath = dbPath;
                Pivot = pivot;
                Head = head;
                CommittedHeadAtPivot = committedHeadAtPivot;
                PivotAccountsChecked = checkedCount;
                PivotAccountsMatched = matchedCount;
                ForwardExecutedBlocks = executed;
                CommittedHeadAfterCatchup = bundle.Metadata.GetLastBlock();
                OldestDiffBlockAfterCatchup = (await bundle.Diffs.GetOldestDiffBlockAsync()).Value;
                NewestDiffBlockAfterCatchup = (await bundle.Diffs.GetNewestDiffBlockAsync()).Value;
            }

            public void Dispose()
            {
                try { _pool?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _server?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { Bundle?.Dispose(); } catch { }
                try { if (_dbPath != null && Directory.Exists(_dbPath)) Directory.Delete(_dbPath, true); } catch { }
            }
        }
    }
}
