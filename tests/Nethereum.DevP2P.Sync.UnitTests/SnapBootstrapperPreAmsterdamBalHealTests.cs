using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPreAmsterdamBalHealTests
    {
        private sealed class NullBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class FixedActivations : IChainActivations
        {
            private readonly HardforkName _fork;
            public FixedActivations(HardforkName fork) => _fork = fork;
            public HardforkName ResolveAt(long blockNumber, ulong timestamp) => _fork;
        }

        private sealed class EmptyPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class HandlerScheduler : IFetchRequestScheduler
        {
            private readonly ISnapRequestHandler _snap;
            public HandlerScheduler(ISnapRequestHandler snap) => _snap = snap;
            private static Task<T> Unsupported<T>() => Task.FromException<T>(new NotSupportedException("no block history in this test"));
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => Unsupported<List<BlockHeader>>();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => Unsupported<List<BlockBody>>();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => Unsupported<BodyFetchResult>();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => Unsupported<List<List<Receipt>>>();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _snap.GetAccountRangeAsync(new GetAccountRangeMessage { RootHash = stateRoot, StartingHash = startingHash, LimitHash = limitHash, ResponseBytes = responseBytes }, ct);
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _snap.GetStorageRangesAsync(new GetStorageRangesMessage { RootHash = stateRoot, AccountHashes = accountHashes, StartingHash = startingHash ?? Array.Empty<byte>(), LimitHash = limitHash ?? Array.Empty<byte>(), ResponseBytes = responseBytes }, ct);
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => _snap.GetByteCodesAsync(new GetByteCodesMessage { Hashes = codeHashes, ResponseBytes = responseBytes }, ct);
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => _snap.GetTrieNodesAsync(new GetTrieNodesMessage { RootHash = stateRoot, Paths = paths, ResponseBytes = responseBytes }, ct);
        }

        private sealed class HighRangeHeldOncePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private int _highRangeCalls;

            public HighRangeHeldOncePeer(ISnapPeer inner) => _inner = inner;

            public TaskCompletionSource<bool> HighRangeHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                if (r.StartingHash is { Length: > 0 } && r.StartingHash[0] >= 0x80 && Interlocked.Increment(ref _highRangeCalls) == 1)
                {
                    HighRangeHeld.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default) => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default) => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default) => _inner.GetTrieNodesAsync(r, ct);
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages = new();
            public IReadOnlyList<string> Messages => _messages.ToList();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _messages.Enqueue(formatter(state, exception));
        }

        private static Account Eoa(int balance) => new Account
        {
            Nonce = (EvmUInt256)1,
            Balance = (EvmUInt256)balance,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private static SnapSyncAccountTask Chunk(byte first, byte last) => new SnapSyncAccountTask
        {
            Next = Enumerable.Range(0, 32).Select(i => i == 0 ? first : (byte)0).ToArray(),
            Last = Enumerable.Range(0, 32).Select(i => i == 0 ? last : (byte)0xff).ToArray(),
            StorageCompleted = Array.Empty<byte[]>(),
            SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current),
        };

        private static byte[] StateRoot(InMemoryContentNodeStore store, byte[] low, int lowBalance, byte[] high, int highBalance)
        {
            var trie = new PatriciaTrie(store);
            trie.Put(low, new AccountEncoder().Encode(Eoa(lowBalance)));
            trie.Put(high, new AccountEncoder().Encode(Eoa(highBalance)));
            trie.SaveDirtyNodesToStorage();
            return trie.Root.GetHash();
        }

        private static async Task<(Task<SnapBootstrapper.Result> Run, RecordingLogger Log, BlockHeader Moved)> StartAsync(HardforkName fork)
        {
            var store = new InMemoryContentNodeStore();
            var low = Enumerable.Repeat((byte)0x10, 32).ToArray();
            var high = Enumerable.Repeat((byte)0x90, 32).ToArray();
            var rootA = StateRoot(store, low, 51, high, 52);
            var rootB = StateRoot(store, low, 54, high, 53);
            var headerA = new BlockHeader { BlockNumber = 400, StateRoot = rootA, ParentHash = new byte[32] };
            var headerB = new BlockHeader { BlockNumber = 410, StateRoot = rootB, ParentHash = new byte[32] };
            var hashA = Sha3Keccack.Current.CalculateHash(new byte[] { 0xA4 });
            var hashB = Sha3Keccack.Current.CalculateHash(new byte[] { 0xB4 });

            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(headerA, hashA);
            await bundle.Blocks.SaveAsync(headerB, hashB);
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 400,
                PivotBlockHash = hashA,
                HealTargetRoot = new byte[32],
                Tasks = new[] { Chunk(0x00, 0x7f), Chunk(0x80, 0xff) },
                Counters = SnapSyncCounters.Zero,
            });

            var handler = new PatriciaSnapRequestHandler(store, new NullBytecodeStore());
            var peer = new HighRangeHeldOncePeer(new InProcessSnapPeer(handler));
            bool Moved() => peer.HighRangeHeld.Task.IsCompleted;
            var log = new RecordingLogger();

            var run = SnapBootstrapper.RunAsync(
                bundle, peer, headerA, hashA, log,
                new SnapRunOptions
                {
                    Scheduler = new HandlerScheduler(handler),
                    Pool = new EmptyPool(),
                    Activations = new FixedActivations(fork),
                    BalHealEnabled = true,
                    RunBackfill = false,
                    AccountConcurrency = 1,
                    RootRefreshIntervalMs = 15,
                    PivotRefresher = (force, ct) => Task.FromResult<(BlockHeader Header, byte[] Hash)?>(Moved() ? (headerB, hashB) : (headerA, hashA)),
                },
                new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
            return (run, log, headerB);
        }

        [Fact]
        public async Task Given_BalHealEnabledAndAPreAmsterdamBootPivot_When_ThePivotMoves_Then_TheRunIsSnap1ForTheWholeBootstrapAndHealsAtTheEnd()
        {
            var (run, log, moved) = await StartAsync(HardforkName.Osaka);

            var result = await run;

            Assert.True(result.Ran, result.SkipReason);
            Assert.Equal(410UL, result.PivotBlockNumber);
            Assert.Equal(moved.StateRoot.ToHex(), result.PivotStateRoot.ToHex());
            Assert.Contains(log.Messages, m => m.Contains("snap.phase2.pivot_move new_root=", StringComparison.Ordinal));
            Assert.Contains(log.Messages, m => m.Contains("from=Phase2 to=Phase3", StringComparison.Ordinal));
            Assert.DoesNotContain(log.Messages, m => m.Contains("snap.bal_catchup", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_BalHealEnabledAndAnAmsterdamBootPivot_When_RunAsyncStarts_Then_ItTakesTheSnap2Path()
        {
            var (run, log, _) = await StartAsync(HardforkName.Amsterdam);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => run);

            Assert.Contains("snap/2 needs a bundle that generates the trie from flat state", ex.Message);
            Assert.DoesNotContain(log.Messages, m => m.Contains("snap.phase2", StringComparison.Ordinal));
        }
    }
}
