using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncOrchestratorReturnedPivotFloorTests
    {
        private const ulong SavedPivot = 980;
        private const ulong LaidTip = 990;
        private const ulong CanonicalTipBlock = 1100;
        private static readonly ulong TrailedBelowSaved = LaidTip - SnapSyncOrchestrator.PivotServeTrailDistance;

        private sealed class FixedTipSource : ICanonicalStateRootSource
        {
            public string Name => "fixed-tip";
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(new CanonicalTip { BlockNumber = CanonicalTipBlock, BlockHash = Hash(CanonicalTipBlock), StateRoot = Root(CanonicalTipBlock) });
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

        private sealed class NoPeersScheduler : IFetchRequestScheduler
        {
            private static Task<T> NoPeers<T>() => Task.FromException<T>(new InvalidOperationException("no peers"));
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => NoPeers<List<BlockHeader>>();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => NoPeers<List<BlockBody>>();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => NoPeers<BodyFetchResult>();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => NoPeers<List<List<Receipt>>>();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => NoPeers<AccountRangeMessage>();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => NoPeers<StorageRangesMessage>();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => NoPeers<ByteCodesMessage>();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => NoPeers<TrieNodesMessage>();
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

        private static byte[] Hash(ulong n)
        {
            var h = new byte[32];
            h[0] = 0x10;
            h[30] = (byte)(n >> 8);
            h[31] = (byte)n;
            return h;
        }

        private static byte[] Root(ulong n) => Enumerable.Repeat((byte)(n & 0xFF), 32).ToArray();

        private static async Task<IReadOnlyList<string>> FetchPivotWithSavedStateAsync(bool balHealEnabled)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            for (ulong n = 0; n <= LaidTip; n++)
                await bundle.Blocks.SaveAsync(new BlockHeader
                {
                    BlockNumber = n,
                    StateRoot = Root(n),
                    ParentHash = n > 0 ? Hash(n - 1) : new byte[32],
                }, Hash(n));
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, LaidTip));
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = SavedPivot,
                PivotBlockHash = Hash(SavedPivot),
                HealTargetRoot = new byte[32],
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });

            var log = new RecordingLogger();
            var tip = new FixedTipSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await SnapSyncOrchestrator.RunAsync(
                    bundle, new EmptyPool(), new NoPeersScheduler(), tip, activations: null, log,
                    new SnapSyncOrchestratorOptions
                    {
                        HeaderFollow = new HeaderFollowService(tip, (from, hash, to, b, ct, above) => Task.FromResult<WalkerOutcome>(null)),
                        RunHistoryBackfill = false,
                        BalHealEnabled = balHealEnabled,
                    },
                    cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
            return log.Messages;
        }

        [Fact]
        public async Task Given_ASavedPivotAndLaidHeadersLaggingTheTip_When_TheOrchestratorFetchesAPivotWithBalHealEnabled_Then_ItWaitsInsteadOfReturningAPivotBelowTheSavedOne()
        {
            var messages = await FetchPivotWithSavedStateAsync(balHealEnabled: true);

            Assert.DoesNotContain(messages, m => m.Contains("pivot anchored on verified local chain", StringComparison.Ordinal));
            Assert.Contains(messages, m => m.Contains("snap.bootstrap.pivot_wait", StringComparison.Ordinal)
                                           && m.Contains($"trailed_pivot={TrailedBelowSaved}", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_ASavedPivotAndLaidHeadersLaggingTheTip_When_TheOrchestratorFetchesAPivotWithBalHealDisabled_Then_ItReturnsTheLowerPivotAsToday()
        {
            var messages = await FetchPivotWithSavedStateAsync(balHealEnabled: false);

            Assert.Contains(messages, m => m.Contains($"pivot anchored on verified local chain block={TrailedBelowSaved}", StringComparison.Ordinal));
        }
    }
}
