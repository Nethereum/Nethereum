using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Chain.TestData.Vectors;
using Xunit;
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
    public class WireSnapDivergenceRecoveryTests
    {
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

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

        [Fact]
        public async Task Follower_SnapsToPivot_ForwardExecutesWithNodeHistory_ThenRewindsViaNodeHistory()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 30);
            await new WorkloadV1().BuildAsync(sequencer);

            for (var i = 0; i < 10; i++)
            {
                var from = sequencer.Accounts.All[i % 8];
                var to = sequencer.Accounts.All[(i + 1) % 8];
                sequencer.QueueTransfer(from, to.Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            await using var server = await WireServerNode.StartAsync(sequencer);
            var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(50);
            var pivotHash = await server.Bundle.Blocks.GetHashByNumberAsync(50);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "follower did not establish a snap-capable peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var snapPeer = new SchedulerSnapPeer(scheduler);

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-snaprec-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(
                    dbPath,
                    journalOptions: HistoricalStateOptions.FullArchive,
                    storageOptions: new RocksDbStorageOptions
                    {
                        DatabasePath = dbPath,
                        PathKeyedState = true,
                        TrieNodeHistoryBlocks = 128,
                        TrieNodeHistoryIndex = true,
                    });
                Assert.NotNull(bundle.NodeCommitBlockSource);
                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var snap = await SnapBootstrapper.RunAsync(bundle, snapPeer, pivotHeader, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { Scheduler = scheduler, RunBackfill = false, Activations = activations, Pool = pool }, cts.Token);
                Assert.True(snap.Ran, snap.SkipReason);
                await snap.StateCompaction.ConfigureAwait(false);

                var headers = await scheduler.FetchHeadersAsync(51, 10, cts.Token);
                Assert.Equal(10, headers.Count);
                var hashes = headers.Select(h => RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(h)).ToList();
                var bodies = await scheduler.FetchBodiesAsync(hashes, cts.Token);
                Assert.Equal(10, bodies.Count);

                var chainConfig = new ChainConfig { ChainId = sequencer.ChainId, BaseFee = BigInteger.Zero, Coinbase = sequencer.SequencerAddress };
                var hardforkConfig = chainConfig.GetHardforkConfig();
                using var stack = ExecutionStackFactory.Build(bundle, activations,
                    _ => chainConfig, _ => hardforkConfig,
                    new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });
                var importer = stack.Importer;

                for (var i = 0; i < headers.Count; i++)
                {
                    var result = await importer.ImportAsync(headers[i], bodies[i].Transactions, null, null);
                    Assert.True(result.RootMatches, $"forward block {headers[i].BlockNumber} state root diverged");
                    bundle.Metadata.Commit((ulong)headers[i].BlockNumber, result.BlockHash);
                }
                Assert.Equal(60UL, bundle.Metadata.GetLastBlock());

                const ulong target = 55;
                var coordinator = new RewindCoordinator(bundle);
                var rewind = await coordinator.RewindToAsync(target, RewindPolicy.JournalFirstThenSnapshot, cts.Token);

                Assert.Equal(RewindOutcome.NodeHistoryUsed, rewind.Outcome);
                Assert.Equal(target, bundle.Metadata.GetLastBlock());

                var targetHeader = await bundle.Blocks.GetByNumberAsync(target);
                var trie = PatriciaTrie.LoadFromStorage(targetHeader.StateRoot, bundle.StateTrieNodes);
                Assert.Equal(targetHeader.StateRoot, trie.Root.GetHash());
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
