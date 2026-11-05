using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM;
using Xunit;
using Xunit.Abstractions;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    [Collection(Snap2LiveHarness.Collection)]
    public class WireSnapStatelessPeerTests
    {
        private const int TailBlocks = 120;
        private const ulong FirstTipBehindHead = 60;
        private static readonly TimeSpan StatelessWindow = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(6);
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

        private readonly ITestOutputHelper _out;
        public WireSnapStatelessPeerTests(ITestOutputHelper @out) => _out = @out;

        private sealed class MovableTipSource : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _first;
            private readonly CanonicalTip _moved;
            private volatile bool _hasMoved;

            public MovableTipSource(CanonicalTip first, CanonicalTip moved)
            {
                _first = first;
                _moved = moved;
            }

            public void Move() => _hasMoved = true;
            public string Name => "MovableTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(_hasMoved ? _moved : _first);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult(((byte[])null, (byte[])null));
        }

        private sealed class ForwardingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
        {
            private readonly Microsoft.Extensions.Logging.ILogger _target;
            public ForwardingLogger(Microsoft.Extensions.Logging.ILogger target) => _target = target;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _target.Log(logLevel, eventId, state, exception, formatter);
        }

        private static async Task<CanonicalTip> TipAtAsync(PathKeyedWorkloadServer server, ulong block)
            => new CanonicalTip
            {
                BlockNumber = block,
                BlockHash = await server.Bundle.Blocks.GetHashByNumberAsync(block),
                StateRoot = (await server.Bundle.Blocks.GetByNumberAsync(block)).StateRoot,
            };

        [Fact]
        public async Task Given_Snap1Phase2AgainstAServerThatAnswersEveryStateRequestEmptyAtTheFirstPivot_When_ThePivotMoves_Then_Phase2CompletesAtTheNewRootWithAtMostOneRequestPerKindAtTheFirstRootAndNoPeerDrop()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 50);
            await new WorkloadV1().BuildAsync(sequencer);
            for (var i = 0; i < TailBlocks; i++)
            {
                sequencer.QueueTransfer(sequencer.Accounts.All[i % 8], sequencer.Accounts.All[(i + 1) % 8].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            using var server = await PathKeyedWorkloadServer.CreateAsync(sequencer, pathKeyed: false);
            var firstTip = await TipAtAsync(server, server.Head - FirstTipBehindHead);
            var movedTip = await TipAtAsync(server, server.Head);
            var firstPivotRoot = (await server.Bundle.Blocks.GetByNumberAsync(firstTip.BlockNumber - SnapSyncOrchestrator.PivotServeTrailDistance)).StateRoot;
            EmptyAtRootSnapHandler emptyAtFirstPivot = null;
            await server.StartWire(inner => emptyAtFirstPivot = new EmptyAtRootSnapHandler(inner, firstPivotRoot));

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0),
                trustedDialKeys: new[] { server.Enode });
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)), "no snap peer");
            var log = new CapturingLogger();
            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions(), logger: new ForwardingLogger<FetchRequestScheduler>(log));
            var tip = new MovableTipSource(firstTip, movedTip);

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-stateless-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath, PathKeyedState = true });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath);
                using var cts = new CancellationTokenSource(RunTimeout);
                var run = SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, tip, new FixedChainActivations(HardforkNames.Parse("prague")), log,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true, RunHistoryBackfill = false, RootRefreshIntervalMs = 50, PivotStaleDistanceBlocks = 20 },
                    cts.Token);

                Assert.True(await WaitUntilAsync(() => emptyAtFirstPivot.TotalEmptyReplies > 0, TimeSpan.FromSeconds(60)),
                    "the server never answered a state request at the first pivot");
                await Task.Delay(StatelessWindow);
                var accountRangesAtFirstPivot = emptyAtFirstPivot.EmptyReplies(nameof(EmptyAtRootSnapHandler.GetAccountRangeAsync));
                var storageRangesAtFirstPivot = emptyAtFirstPivot.EmptyReplies(nameof(EmptyAtRootSnapHandler.GetStorageRangesAsync));
                var trieNodesAtFirstPivot = emptyAtFirstPivot.EmptyReplies(nameof(EmptyAtRootSnapHandler.GetTrieNodesAsync));
                tip.Move();
                var result = await run;
                await result.HistoryBackfill;

                _out.WriteLine($"empty replies at the first pivot over {StatelessWindow}: account_range={accountRangesAtFirstPivot} storage_range={storageRangesAtFirstPivot} trie_nodes={trieNodesAtFirstPivot}");
                Assert.True(result.Ran, result.SkipReason);
                Assert.True(result.PivotBlockNumber > firstTip.BlockNumber - SnapSyncOrchestrator.PivotServeTrailDistance,
                    $"Phase 2 finished at pivot {result.PivotBlockNumber}, not at the moved pivot");
                Assert.True(accountRangesAtFirstPivot <= 1, $"the account range at the first pivot was asked {accountRangesAtFirstPivot} times");
                Assert.True(storageRangesAtFirstPivot <= 1, $"a storage range at the first pivot was asked {storageRangesAtFirstPivot} times");
                Assert.True(trieNodesAtFirstPivot <= 1, $"trie nodes at the first pivot were asked {trieNodesAtFirstPivot} times");
                Assert.Equal(0, log.Count("snap.peer.quarantine.drop"));
                Assert.Equal(0, log.Count("snap.bootstrap.retry"));
            }
            finally
            {
                foreach (var m in log.Messages.Where(m => m.Contains("stateless", StringComparison.Ordinal) || m.Contains("peer", StringComparison.Ordinal) || m.Contains("pivot", StringComparison.Ordinal))) _out.WriteLine(m);
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
