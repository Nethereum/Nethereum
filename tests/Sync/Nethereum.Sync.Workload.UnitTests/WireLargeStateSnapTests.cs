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
using Nethereum.Util;
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
    public class WireLargeStateSnapTests
    {
        private const int Accounts = 2200;
        private const int Blocks = 70;
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

        private static async Task<bool> WaitUntilAsync(Func<bool> c, TimeSpan t)
        {
            var d = DateTime.UtcNow + t;
            while (DateTime.UtcNow < d) { if (c()) return true; await Task.Delay(100); }
            return c();
        }

        [Fact]
        public async Task Follower_SnapsLargeState_OverManyRounds_ViaSharedOrchestrator()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: Accounts);
            for (var i = 0; i < Blocks; i++)
            {
                sequencer.QueueTransfer(sequencer.Accounts.All[i % 8], sequencer.Accounts.All[(i + 1) % 8].Address, Eth(1));
                await sequencer.ProduceBlockAsync();
            }

            await using var server = await WireServerNode.StartAsync(sequencer, snapResponseLimit: 8 * 1024);
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

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-large-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));

                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                Assert.True(result.AccountCount > 2000, $"only {result.AccountCount} accounts synced over many rounds");

                var pivot = result.PivotBlockNumber;
                var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(pivot);

                for (var n = 0UL; n <= pivot; n++)
                    Assert.Equal(
                        await server.Bundle.Blocks.GetHashByNumberAsync(n),
                        await bundle.Blocks.GetHashByNumberAsync(n));

                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                foreach (var account in sequencer.Accounts.All.Take(24))
                {
                    var synced = await recovered.GetAccountAsync(account.Address);
                    Assert.NotNull(synced);
                    var expectedLeaf = serverAtPivot.Get(Sha3Keccack.Current.CalculateHash(account.Address.HexToByteArray()));
                    Assert.NotNull(expectedLeaf);
                    Assert.Equal(accountDecoder.Decode(expectedLeaf).Balance, synced.Balance);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
