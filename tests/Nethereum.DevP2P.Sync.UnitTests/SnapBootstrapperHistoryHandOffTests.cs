using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperHistoryHandOffTests
    {
        private sealed class NullBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
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

        private sealed class BlockingHistoryScheduler : IFetchRequestScheduler
        {
            private int _live;
            private int _maxLive;

            public int LiveRequests => Volatile.Read(ref _live);

            public int MaxLiveRequests => Volatile.Read(ref _maxLive);

            public TaskCompletionSource FirstRequestIssued { get; } =
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => BlockUntilCancelledAsync<List<BlockHeader>>(ct);

            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => BlockUntilCancelledAsync<List<BlockBody>>(ct);

            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
                => BlockUntilCancelledAsync<BodyFetchResult>(ct);

            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => BlockUntilCancelledAsync<List<List<Receipt>>>(ct);

            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new InvalidOperationException("state is served by the snap peer");

            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new InvalidOperationException("state is served by the snap peer");

            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new InvalidOperationException("state is served by the snap peer");

            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new InvalidOperationException("state is served by the snap peer");

            private async Task<T> BlockUntilCancelledAsync<T>(CancellationToken ct)
            {
                var live = Interlocked.Increment(ref _live);
                int max;
                while (live > (max = Volatile.Read(ref _maxLive)) && Interlocked.CompareExchange(ref _maxLive, live, max) != max) { }
                FirstRequestIssued.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return default;
                }
                finally
                {
                    Interlocked.Decrement(ref _live);
                }
            }
        }

        private sealed class SnapPeerThatAnswersOnceHistoryIsInFlight : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly Task _historyInFlight;

            public SnapPeerThatAnswersOnceHistoryIsInFlight(ISnapPeer inner, Task historyInFlight)
            {
                _inner = inner;
                _historyInFlight = historyInFlight;
            }

            private async Task WaitForHistoryAsync(CancellationToken ct)
            {
                try { await _historyInFlight.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); }
                catch (TimeoutException) { }
            }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            {
                await WaitForHistoryAsync(ct).ConfigureAwait(false);
                return await _inner.GetAccountRangeAsync(request, ct).ConfigureAwait(false);
            }

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                await WaitForHistoryAsync(ct).ConfigureAwait(false);
                return await _inner.GetStorageRangesAsync(request, ct).ConfigureAwait(false);
            }

            public async Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            {
                await WaitForHistoryAsync(ct).ConfigureAwait(false);
                return await _inner.GetByteCodesAsync(request, ct).ConfigureAwait(false);
            }

            public async Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
            {
                await WaitForHistoryAsync(ct).ConfigureAwait(false);
                return await _inner.GetTrieNodesAsync(request, ct).ConfigureAwait(false);
            }
        }

        private static byte[] BuildState(InMemoryContentNodeStore store)
        {
            var keccak = Sha3Keccack.Current;
            var trie = new PatriciaTrie(store);
            for (byte i = 1; i <= 3; i++)
                trie.Put(keccak.CalculateHash(new[] { i }), new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)i,
                    Balance = (EvmUInt256)(i * 10u),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            trie.SaveDirtyNodesToStorage();
            return trie.Root.GetHash();
        }

        [Fact]
        public async Task Given_Snap1WhosePivotHeaderIsMissingAtCommit_When_RunAsyncPropagatesTheException_Then_TheBackfillWasCancelledAndAwaitedFirst()
        {
            var store = new InMemoryContentNodeStore();
            var root = BuildState(store);
            var pivot = new BlockHeader { BlockNumber = 40, StateRoot = root };
            var pivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x40 });
            var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new BlockingHistoryScheduler();
            var peer = new SnapPeerThatAnswersOnceHistoryIsInFlight(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NullBytecodeStore())),
                scheduler.FirstRequestIssued.Task);

            using var testCts = new CancellationTokenSource();
            var run = SnapBootstrapper.RunAsync(
                bundle, peer, pivot, pivotHash, NullLogger.Instance,
                new SnapRunOptions
                {
                    Scheduler = scheduler,
                    Pool = new EmptyPool(),
                    RunBackfill = true,
                    UseBackwardSkeleton = false,
                    AccountConcurrency = 1,
                },
                testCts.Token);

            var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));
            if (finished != run)
            {
                var liveWhenStuck = scheduler.LiveRequests;
                testCts.Cancel();
                try { await run; } catch { }
                Assert.Fail($"RunAsync did not return within 30s: the failed finalize waited on a backfill nobody cancelled (live={liveWhenStuck}).");
            }

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
            Assert.Contains("is not in the store at commit time", thrown.Message);
            Assert.True(scheduler.MaxLiveRequests >= 1, "the Phase-1 backfill never issued a request, so the pin is vacuous");
            Assert.Equal(0, scheduler.LiveRequests);
        }

        private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
        {
            public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Messages.Enqueue(formatter(state, exception));
        }

        [Fact]
        public async Task Given_ABackfillThatNeverObservesCancellation_When_ItIsStopped_Then_TheStopReturnsAtTheBoundAndLogsTheTimeout()
        {
            using var cts = new CancellationTokenSource();
            var neverStops = new TaskCompletionSource<bool>().Task;
            var log = new RecordingLogger();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var stop = SnapBootstrapper.StopBackfillAsync(cts, neverStops, log);
            var finished = await Task.WhenAny(stop, Task.Delay(SnapBootstrapper.BackfillStopTimeout + TimeSpan.FromSeconds(10)));

            Assert.Same(stop, finished);
            Assert.True(cts.IsCancellationRequested);
            Assert.True(sw.Elapsed >= SnapBootstrapper.BackfillStopTimeout - TimeSpan.FromMilliseconds(100), $"the stop returned after {sw.Elapsed}, before the bound");
            Assert.Contains(log.Messages, m => m.StartsWith("snap.phase1.backfill.stop_timeout", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_ABackfillThatFaultsWhileStopping_When_ItIsStopped_Then_TheFaultIsLoggedNotSwallowed()
        {
            using var cts = new CancellationTokenSource();
            var log = new RecordingLogger();

            await SnapBootstrapper.StopBackfillAsync(cts, Task.FromException(new InvalidOperationException("disk gone")), log);

            Assert.Contains(log.Messages, m => m.StartsWith("snap.phase1.backfill.stopped_faulted", StringComparison.Ordinal));
        }
    }
}
