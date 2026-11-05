using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class SnapBootstrapperHealRecoveryE2ETests : IDisposable
    {
        private readonly string _dbPath;

        public SnapBootstrapperHealRecoveryE2ETests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_heal_recovery_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task Heal_SurvivesManyZeroProgressCycles_EachWokenByAPeerJoin_ThenConvergesOnceAPeerServes()
        {
            var (bundle, pivotHeader, pivotHash, leafBlob) = await OpenHealOnlyResumeAsync();
            using var store = bundle;
            var scheduler = new HostileThenServingScheduler(leafBlob, serveAfterCalls: 400);
            var pool = new JoinablePool();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var run = SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance,
                HealOnlyOptions(scheduler, pool), cts.Token);

            var joinsWhileWaiting = 0;
            while (await RequestsStopAsync(scheduler, run, TimeSpan.FromMilliseconds(300)))
            {
                pool.Join(new JoiningPeer());
                joinsWhileWaiting++;
            }
            var result = await run;

            Assert.True(result.Ran);
            Assert.True(joinsWhileWaiting > 10,
                $"expected well over 10 zero-progress cycles, each waiting for a peer join; joins={joinsWhileWaiting}");
            Assert.True(scheduler.TotalCalls > 320,
                $"expected well over 10 hostile cycles (~32 rounds each) before recovery; TotalCalls={scheduler.TotalCalls}");
            Assert.Equal(1UL, bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Heal_AfterAZeroProgressCycle_DoesNotAskTheSameRootAgainUntilAPeerJoins()
        {
            var (bundle, pivotHeader, pivotHash, leafBlob) = await OpenHealOnlyResumeAsync();
            using var store = bundle;
            var scheduler = new HostileThenServingScheduler(leafBlob, serveAfterCalls: int.MaxValue);
            var pool = new JoinablePool();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            var run = SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, NullLogger.Instance,
                HealOnlyOptions(scheduler, pool), cts.Token);

            Assert.True(await RequestsStopAsync(scheduler, run, TimeSpan.FromMilliseconds(500)),
                $"the heal ended during a cycle in which no peer served anything: {run.Status} {run.Exception?.GetBaseException().Message}");
            var callsAfterFirstCycle = scheduler.TotalCalls;
            scheduler.ServeFromNextCall();

            await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.False(run.IsCompleted, "the heal finished without a peer join or a pivot move");
            Assert.Equal(callsAfterFirstCycle, scheduler.TotalCalls);
            Assert.NotEqual(1UL, bundle.Metadata.GetLastBlock());

            pool.Join(new JoiningPeer());
            var result = await run;

            Assert.True(result.Ran);
            Assert.True(scheduler.TotalCalls > callsAfterFirstCycle);
            Assert.Equal(1UL, bundle.Metadata.GetLastBlock());
        }

        private async Task<(RocksDbChainStoreBundle Bundle, BlockHeader PivotHeader, byte[] PivotHash, byte[] LeafBlob)> OpenHealOnlyResumeAsync()
        {
            var scratch = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(scratch);
            var accountHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xAA });
            var account = new Account
            {
                Nonce = 0,
                Balance = 0UL,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            trie.Put(accountHash, new AccountEncoder().Encode(account));
            trie.SaveNodesToStorage();
            var rootHash = trie.Root.GetHash();
            var leafBlob = scratch.Get(rootHash);

            var bundle = RocksDbChainStoreBundle.Open(_dbPath);

            var pivotHeader = new BlockHeader
            {
                BlockNumber = 1,
                StateRoot = rootHash,
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            var pivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xFE, 0xED });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = pivotHash,
                HealTargetRoot = rootHash,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });

            return (bundle, pivotHeader, pivotHash, leafBlob);
        }

        private static SnapRunOptions HealOnlyOptions(IFetchRequestScheduler scheduler, IPeerPool pool) => new SnapRunOptions
        {
            Scheduler = scheduler,
            Pool = pool,
            PivotRefresher = (_, _) => Task.FromResult<(BlockHeader Header, byte[] Hash)?>(null),
            RunBackfill = false,
        };

        private static async Task<bool> RequestsStopAsync(HostileThenServingScheduler scheduler, Task run, TimeSpan quiet)
        {
            var seen = scheduler.TotalCalls;
            while (!run.IsCompleted)
            {
                await Task.WhenAny(run, Task.Delay(quiet));
                var now = scheduler.TotalCalls;
                if (now > 0 && now == seen) return !run.IsCompleted;
                seen = now;
            }
            return false;
        }

        private sealed class JoinablePool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 1;
            public event EventHandler<IEthPeer> PeerAdded;
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public void Join(IEthPeer peer) => PeerAdded?.Invoke(this, peer);
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class JoiningPeer : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://joining@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 0;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null;
            public event EventHandler<IEthPeer> Disconnected { add { } remove { } }
        }

        private sealed class NeverCalledPeer : ISnapPeer
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("heal-only resume must not touch the snap peer");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
        }

        private sealed class HostileThenServingScheduler : IFetchRequestScheduler
        {
            private readonly byte[] _leafBlob;
            private int _serveAfterCalls;
            private int _totalCalls;
            public int TotalCalls => Volatile.Read(ref _totalCalls);

            public void ServeFromNextCall() => Volatile.Write(ref _serveAfterCalls, TotalCalls);

            public HostileThenServingScheduler(byte[] leafBlob, int serveAfterCalls)
            {
                _leafBlob = leafBlob;
                _serveAfterCalls = serveAfterCalls;
            }

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                var call = Interlocked.Increment(ref _totalCalls);
                var msg = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                if (call > Volatile.Read(ref _serveAfterCalls))
                {
                    msg.Nodes.Add(_leafBlob);
                    for (int i = 1; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                }
                else
                {
                    for (int i = 0; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                }
                return Task.FromResult(msg);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
