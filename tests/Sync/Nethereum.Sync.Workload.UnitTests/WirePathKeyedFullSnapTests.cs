using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.CoreChain.State;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Chain.TestData.Vectors;
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
    public class WirePathKeyedFullSnapTests
    {
        private const int TailBlocks = 50;
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

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();
            public System.Collections.Generic.IReadOnlyCollection<string> Messages => _messages;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel l) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel l, Microsoft.Extensions.Logging.EventId e,
                TState s, Exception ex, Func<TState, Exception, string> f)
                => _messages.Enqueue(f(s, ex) + (ex != null ? " " + ex.Message : ""));
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

        [Fact]
        public async Task Follower_FullSnapIntoPathKeyedStore_StateIsProofGradeReadable_AndHealRepairsWipedStorage()
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

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-pathfull-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath, PathKeyedState = true });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var log = new CapturingLogger();

                var result = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, log,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;

                var pivot = result.PivotBlockNumber;
                var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(pivot);

                for (var n = 0UL; n <= pivot; n++)
                    Assert.Equal(
                        await server.Bundle.Blocks.GetHashByNumberAsync(n),
                        await bundle.Blocks.GetHashByNumberAsync(n));

                var stateTrie = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, bundle.StateTrieNodes);
                var serverAtPivot = PatriciaTrie.LoadFromStorage(pivotHeader.StateRoot, server.Bundle.TrieNodes);
                var accountDecoder = new AccountEncoder();
                (byte[] OwnerHash, byte[] StorageRoot)? contract = null;
                foreach (var kv in await server.Bundle.State.GetAllAccountsAsync())
                {
                    var addressHash = Sha3Keccack.Current.CalculateHash(kv.Key.HexToByteArray());
                    var leaf = stateTrie.Get(addressHash);
                    Assert.True(leaf != null, $"account {kv.Key} missing from the path-keyed state trie");
                    var synced = accountDecoder.Decode(leaf);
                    var expected = accountDecoder.Decode(serverAtPivot.Get(addressHash));
                    Assert.Equal(expected.Balance, synced.Balance);
                    Assert.Equal(expected.Nonce, synced.Nonce);

                    if (contract == null && synced.StateRoot != null
                        && !ByteUtil.AreEqual(synced.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                        contract = (addressHash, synced.StateRoot);
                }
                Assert.True(contract.HasValue, "workload must contain at least one contract with storage");

                var (owner, storageRoot) = contract.Value;
                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, bundle.StateTrieNodes, owner);
                Assert.Equal(storageRoot, storageTrie.Root.GetHash());

                var pathStore = Assert.IsType<RocksDbPathTrieNodeStore>(bundle.StateTrieNodes);
                pathStore.DeleteRange(owner);
                var sink = ((IHealNodeSinkProvider)bundle).CreateHealSink();
                Assert.False(sink.HasNode(true, owner, Array.Empty<byte>(), storageRoot), "wipe precondition");

                var healer = new TrieHealer(scheduler, sink, bundle.StateTrieNodes, NullLogger.Instance);
                var heal = await healer.HealAsync(
                    pivotHeader.StateRoot, new[] { (owner, storageRoot) }, pivotBlock: pivot, ct: cts.Token);

                Assert.True(heal.Matched, "seeded heal must converge");
                var healedStorage = PatriciaTrie.LoadFromStorage(storageRoot, bundle.StateTrieNodes, owner);
                Assert.Equal(storageRoot, healedStorage.Root.GetHash());

                Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
                Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
                var reconciler = Assert.IsAssignableFrom<IFlatStateReconciler>(bundle);
                Assert.Empty(reconciler.GetPersistedDamage());
                var verify = await reconciler.VerifyFlatStateAsync(
                    pivotHeader.StateRoot,
                    msg => log.LogInformation("snap.flat.verify.post {Progress}", msg),
                    cts.Token);
                Assert.Equal(0, verify.TotalRepairs);
                Assert.DoesNotContain(log.Messages, m => m.Contains("snap.flat.repair attempt=", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }
    }
}
