using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.State;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
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
    public class WireSnapTests
    {
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
        public async Task Follower_SnapSyncs_FromServer_OverTheWire_AndMatchesEveryAccount()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 50);
            await new WorkloadV1().BuildAsync(sequencer);
            await using var server = await WireServerNode.StartAsync(sequencer);

            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var pivotHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);

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

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-snap-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dbPath);
                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapBootstrapper.RunAsync(
                    bundle, snapPeer, pivotHeader, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { Scheduler = scheduler, RunBackfill = false, Activations = activations, Pool = pool },
                    cts.Token);

                Assert.True(result.Ran, result.SkipReason);
                await result.StateCompaction.ConfigureAwait(false);
                Assert.True(result.AccountCount > 50, $"only {result.AccountCount} accounts synced");

                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                foreach (var kv in await server.Bundle.State.GetAllAccountsAsync())
                {
                    var synced = await recovered.GetAccountAsync(kv.Key);
                    Assert.NotNull(synced);
                    Assert.Equal(kv.Value.Balance, synced.Balance);
                    Assert.Equal(kv.Value.Nonce, synced.Nonce);
                    Assert.Equal(kv.Value.CodeHash, synced.CodeHash);
                    Assert.Equal(kv.Value.StateRoot, synced.StateRoot);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
