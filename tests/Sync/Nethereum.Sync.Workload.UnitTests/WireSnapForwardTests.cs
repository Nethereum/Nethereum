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
using Nethereum.CoreChain.State;
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
    public class WireSnapForwardTests
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
        public async Task Follower_SnapsToMidpoint_ThenForwardExecutesOverWire_AllBlocksMatch()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 30);
            await new WorkloadV1().BuildAsync(sequencer);

            for (var i = 0; i < 50; i++)
            {
                var from = sequencer.Accounts.All[i % 8];
                var to = sequencer.Accounts.All[(i + 1) % 8];
                sequencer.QueueTransfer(from, to.Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            await using var server = await WireServerNode.StartAsync(sequencer);
            var tip = (ulong)await server.Bundle.Blocks.GetHeightAsync();
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

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-snapfwd-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dbPath);
                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var snap = await SnapBootstrapper.RunAsync(bundle, snapPeer, pivotHeader, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { Scheduler = scheduler, RunBackfill = false, Activations = activations, Pool = pool }, cts.Token);
                Assert.True(snap.Ran, snap.SkipReason);
                await snap.StateCompaction.ConfigureAwait(false);

                var headers = await scheduler.FetchHeadersAsync(51, 50, cts.Token);
                Assert.Equal(50, headers.Count);
                var hashes = headers.Select(h => RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(h)).ToList();
                var bodies = await scheduler.FetchBodiesAsync(hashes, cts.Token);
                Assert.Equal(50, bodies.Count);

                var chainConfig = new ChainConfig { ChainId = sequencer.ChainId, BaseFee = BigInteger.Zero, Coinbase = sequencer.SequencerAddress };
                var hardforkConfig = chainConfig.GetHardforkConfig();
                var fallback = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot, backfill: true);
                var calc = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
                var engine = new BlockExecutor(fallback, bundle.Blocks, activations,
                    chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                    stateRootCalculator: calc, rewardPolicy: NoRewardPolicy.Instance, trieNodeStore: bundle.TrieNodes);
                var importer = new BlockImporter(engine, bundle.Blocks, fallback, bundle.Transactions, bundle.Receipts, bundle.Logs, uncleStore: bundle.Uncles);

                for (var i = 0; i < headers.Count; i++)
                {
                    var result = await importer.ImportAsync(headers[i], bodies[i].Transactions, null, null);
                    Assert.True(result.RootMatches, $"forward block {headers[i].BlockNumber} state root diverged");
                    bundle.Metadata.Commit((ulong)headers[i].BlockNumber, result.BlockHash);
                }

                Assert.Equal(
                    await server.Bundle.Blocks.GetHashByNumberAsync(tip),
                    await bundle.Blocks.GetHashByNumberAsync(tip));
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
