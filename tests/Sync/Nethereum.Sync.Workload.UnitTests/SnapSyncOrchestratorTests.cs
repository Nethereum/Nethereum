using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SnapSyncOrchestratorTests
    {
        private const int TailBlocks = 50;
        private const ulong TipBeyondPivot = 1_000;
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

        private sealed class TrustedTipSource : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _tip;
            public TrustedTipSource(ulong number, byte[] hash, byte[] stateRoot)
                => _tip = new CanonicalTip { BlockNumber = number, BlockHash = hash, StateRoot = stateRoot };
            public string Name => "TestTrustedTip";
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
                await Task.Delay(50);
            }
            return condition();
        }

        [Fact]
        public async Task Given_NoHeaderFollowAndARocksDbBundleBehindTheTip_When_TheOrchestratorRuns_Then_ItReturnsAndTheValveNeverYields()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 50);
            await new WorkloadV1().BuildAsync(sequencer);
            for (var i = 0; i < TailBlocks; i++)
            {
                sequencer.QueueTransfer(sequencer.Accounts.All[i % 8], sequencer.Accounts.All[(i + 1) % 8].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            await using var server = await WireServerNode.StartAsync(sequencer);
            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)), "no snap peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var canonical = new TrustedTipSource(head, headHash, headHeader.StateRoot);

            var dbPath = Path.Combine(Path.GetTempPath(), "orchestrator-valve-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);

                var yielded = 0;
                using var sampling = new CancellationTokenSource();
                var sampler = Task.Run(async () =>
                {
                    while (!sampling.IsCancellationRequested)
                    {
                        if (bundle.ShouldYieldToExecution()) Interlocked.Increment(ref yielded);
                        try { await Task.Delay(5, sampling.Token); }
                        catch (OperationCanceledException) { }
                    }
                });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token).WaitAsync(TimeSpan.FromSeconds(90));
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill.WaitAsync(TimeSpan.FromSeconds(60));

                sampling.Cancel();
                await sampler.WaitAsync(TimeSpan.FromSeconds(10));

                bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
                {
                    SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                    Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = result.PivotBlockNumber + TipBeyondPivot, Tail = 0, Next = 0 } },
                });

                Assert.Equal(0, Volatile.Read(ref yielded));
                Assert.False(bundle.ShouldYieldToExecution());

                bundle.SetYieldToExecution(true);
                Assert.True(bundle.ShouldYieldToExecution(), "the bundle is not behind the tip, so the unarmed result proves nothing");
                bundle.SetYieldToExecution(false);
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
