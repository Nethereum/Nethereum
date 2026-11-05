using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPendingDamageHealTests
    {
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();

        private static byte[] Fill(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private static BlockHeader Header(int blockNumber, byte[] stateRoot)
            => new() { BlockNumber = blockNumber, StateRoot = stateRoot };

        private sealed class PendingDamageBundle : IChainStoreBundle, IFlatStateReconciler, IHealNodeSinkProvider
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
            private List<(byte[] AccountHash, byte[] StorageRoot)> _damage;
            public readonly CountingHealNodeSink Sink;
            public readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> ReconcileRoots = new();

            public PendingDamageBundle(IEnumerable<(byte[] AccountHash, byte[] StorageRoot)> damage)
            {
                _damage = damage.ToList();
                Sink = new CountingHealNodeSink(new HashHealNodeSink((INodeBlobStore)_inner.TrieNodes));
            }

            public IHealNodeSink CreateHealSink() => Sink;

            public IStateStore State => new NoFlatWriterStateStore(_inner.State);
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();

            public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct)
            {
                ReconcileRoots.Enqueue(stateRoot);
                return Task.FromResult(EmptyReconcileResult());
            }
            public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
                => Task.FromResult(EmptyReconcileResult());
            public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage() => _damage;
            public void ClearPersistedDamage() => _damage = new List<(byte[], byte[])>();
            public IReadOnlyList<byte[]> GetPersistedMissingCode() => Array.Empty<byte[]>();
            public void ClearPersistedMissingCode() { }

            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();

            private static FlatStateReconcileResult EmptyReconcileResult() => new(0, 0, 0, 0, 0, 0, 0, 0);
        }

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class WindowedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly byte[] _servableRoot;
            public int StaleTrieNodeCalls;
            public int ServedTrieNodeCalls;
            public int AccountRangeCalls;
            public readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> TrieNodeRoots = new();
            public int TargetRootChanges;
            public TimeSpan FirstResponseDelay;
            private int _responses;

            public void OnTargetRootChanged() => Interlocked.Increment(ref TargetRootChanges);

            public WindowedScheduler(PatriciaSnapRequestHandler handler, byte[] servableRoot)
            {
                _handler = handler;
                _servableRoot = servableRoot;
            }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                TrieNodeRoots.Enqueue(stateRoot);
                if (Interlocked.Increment(ref _responses) == 1 && FirstResponseDelay > TimeSpan.Zero)
                    await Task.Delay(FirstResponseDelay, ct);
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                {
                    Interlocked.Increment(ref StaleTrieNodeCalls);
                    var empty = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                    for (int i = 0; i < paths.Count; i++) empty.Nodes.Add(Array.Empty<byte>());
                    return empty;
                }
                Interlocked.Increment(ref ServedTrieNodeCalls);
                return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    Paths = paths,
                    ResponseBytes = responseBytes,
                });
            }

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref AccountRangeCalls);
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                    throw new FetchRequestFailedException("stale root not servable", null);
                return await _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    StartingHash = startingHash,
                    LimitHash = limitHash,
                    ResponseBytes = responseBytes,
                });
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private sealed class ServedContract
        {
            public readonly byte[] AccountHash = Keccak.CalculateHash(new byte[] { 0x03, 0xCC });
            public readonly byte[] StorageRoot;
            public readonly byte[] Root;
            public readonly WindowedScheduler Scheduler;
            private readonly InMemoryContentNodeStore _serverStore = new();

            public ServedContract()
            {
                var serverStore = _serverStore;
                var storageTrie = new PatriciaTrie(serverStore, Hp);
                for (byte i = 1; i <= 3; i++)
                    storageTrie.Put(Keccak.CalculateHash(new byte[] { i }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xB0 + i) }));
                storageTrie.SaveDirtyNodesToStorage();
                StorageRoot = storageTrie.Root.GetHash();
                var accountTrie = new PatriciaTrie(serverStore, Hp);
                accountTrie.Put(AccountHash, new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1,
                    Balance = (EvmUInt256)1000UL,
                    StateRoot = StorageRoot,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
                accountTrie.SaveDirtyNodesToStorage();
                Root = accountTrie.Root.GetHash();
                Scheduler = new WindowedScheduler(new PatriciaSnapRequestHandler(serverStore, new NullBytecodes()), servableRoot: Root);
            }

            public (byte[] AccountHash, byte[] StorageRoot)[] Holes => new[] { (AccountHash, StorageRoot) };

            public void StoreIn(IChainStoreBundle bundle)
            {
                var store = (INodeBlobStore)bundle.TrieNodes;
                foreach (var node in _serverStore.Storage) store.Put(node.Key, node.Value);
            }
        }

        private static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> Answers(BlockHeader header, byte fill)
            => (_, _) => Task.FromResult<(BlockHeader, byte[])?>((header, Fill(fill)));

        [Fact]
        public async Task Given_ACertifyThatReportsUnresolvableSubtrees_When_Finalizing_Then_EveryHealTargetIsTheRefreshedPivotRootAndNeverTheCertifiedRoot()
        {
            var served = new ServedContract();
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: 1);
            var certifiedRoot = Fill(0xCD);
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, certifiedRoot), Fill(0x01));
            var log = new MessageLog();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, Answers(Header(600, served.Root), 0x02), null,
                log, cts.Token);

            Assert.NotEmpty(served.Scheduler.TrieNodeRoots);
            Assert.All(served.Scheduler.TrieNodeRoots, root => Assert.Equal(served.Root, root));
            Assert.Equal(new[] { served.Root, served.Root }, bundle.ReconcileRoots.ToArray());
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
            Assert.Equal(0, log.Count(WaitingForPivot));
            Assert.True(served.Scheduler.TargetRootChanges >= 1, "the move from the certified root to the refreshed pivot did not reset the scheduler's per-root state");
        }

        [Fact]
        public async Task Given_AnOwnerWhoseStorageTrieCannotBeLoaded_When_Finalized_Then_TheOwnerIsWipedRefetchedAndFlatIsExact()
        {
            var served = new ServedContract();
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: 1);
            served.StoreIn(bundle);
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, served.Root), Fill(0x01));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, Answers(Header(100, served.Root), 0x01), null,
                NullLogger.Instance, cts.Token);

            Assert.Equal(1, bundle.Sink.ForceWipeCalls);
            Assert.Equal(new[] { served.Root, served.Root }, bundle.ReconcileRoots.ToArray());
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
        }

        [Fact]
        public async Task Given_AConvergedRootTheStoreDoesNotHold_When_Finalizing_Then_ItHealsForwardAndNeverCertifiesOrCommitsTheAbsentRoot()
        {
            var served = new ServedContract();
            var bundle = new PendingDamageBundle(Array.Empty<(byte[], byte[])>());
            var absentRoot = Fill(0xCD);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, new SnapBootstrapper.RollingPivot(Header(100, absentRoot), Fill(0x01)),
                Answers(Header(600, served.Root), 0x02), null, NullLogger.Instance, cts.Token);

            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
            Assert.NotEmpty(served.Scheduler.TrieNodeRoots);
            Assert.All(served.Scheduler.TrieNodeRoots, root => Assert.Equal(served.Root, root));
            Assert.DoesNotContain(bundle.ReconcileRoots, root => root.SequenceEqual(absentRoot));
            Assert.Single(bundle.ReconcileRoots);
        }

        [Fact]
        public async Task Given_AConvergedRootTheStoreHolds_When_Finalizing_Then_CertifyRunsThereWithoutAnyPeerRequest()
        {
            var served = new ServedContract();
            var bundle = new PendingDamageBundle(Array.Empty<(byte[], byte[])>());
            served.StoreIn(bundle);
            var servedHeader = Header(600, served.Root);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, new SnapBootstrapper.RollingPivot(servedHeader, Fill(0x02)), Answers(Header(700, Fill(0xEE)), 0x03), null,
                NullLogger.Instance, cts.Token);

            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
            Assert.Empty(served.Scheduler.TrieNodeRoots);
            Assert.Equal(new[] { served.Root }, bundle.ReconcileRoots.ToArray());
        }

        [Fact]
        public async Task Given_AConvergedRootTheStoreDoesNotHoldAndNoScheduler_When_Finalizing_Then_ItRefusesInsteadOfCommittingTheAbsentRoot()
        {
            var bundle = new PendingDamageBundle(Array.Empty<(byte[], byte[])>());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, null, new SnapBootstrapper.RollingPivot(Header(100, Fill(0xCD)), Fill(0x01)), null, null,
                NullLogger.Instance, cts.Token));

            Assert.Contains("stored account root", refused.Message);
        }

        [Fact]
        public async Task Given_APersistedDamageInventoryFromACrashedRun_When_Finalizing_Then_ItIsDiscardedAndCertifyRunsWithoutAWipeOrAPeerRequest()
        {
            var served = new ServedContract();
            var bundle = new PendingDamageBundle(served.Holes);
            served.StoreIn(bundle);
            var servedHeader = Header(600, served.Root);
            var rollingPivot = new SnapBootstrapper.RollingPivot(servedHeader, Fill(0x02));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, Answers(servedHeader, 0x02), null,
                NullLogger.Instance, cts.Token);

            Assert.Equal(0, bundle.Sink.ForceWipeCalls);
            Assert.Empty(served.Scheduler.TrieNodeRoots);
            Assert.Empty(bundle.GetPersistedDamage());
            Assert.Equal(new[] { served.Root }, bundle.ReconcileRoots.ToArray());
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
        }

        [Fact]
        public async Task Given_AnUnresolvableCertifyOnAnEmptyState_When_TheRepairHeals_Then_TheHealAtTheEmptyRootMatchesAtOnceWithoutAPeerRequest()
        {
            var empty = new UnservedRepair();
            var emptyHeader = empty.ServedHeader;
            var bundle = new ScriptedReconcileBundle(new[] { (Keccak.CalculateHash(new byte[] { 0x01, 0xAA }), Fill(0xAB)) }, unresolvableCalls: 1);
            var log = new MessageLog();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, empty.Scheduler, new SnapBootstrapper.RollingPivot(emptyHeader, Fill(0x02)), Answers(emptyHeader, 0x02), null,
                log, cts.Token);

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, finalPivot.Header.StateRoot);
            Assert.Equal(2, bundle.ReconcileCalls);
            Assert.Empty(empty.Scheduler.TrieNodeRoots);
            Assert.Equal(0, log.Count(WaitingForPivot));
        }

        [Fact]
        public async Task Given_ThePivotMovesDuringTheRepairHealWithoutARotation_When_TheHealMatches_Then_CertifyRunsAtTheRootTheHealConvergedTo()
        {
            var served = new ServedContract();
            served.Scheduler.FirstResponseDelay = TimeSpan.FromSeconds(13);
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: 0);
            var servedHeader = Header(600, served.Root);
            var nearbyHeader = Header(610, Fill(0xEE));
            var refreshes = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> movesAfterFirstRefresh = (_, _) =>
                Task.FromResult<(BlockHeader, byte[])?>(Interlocked.Increment(ref refreshes) == 1
                    ? (servedHeader, Fill(0x02))
                    : (nearbyHeader, Fill(0x03)));
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, Fill(0xCD)), Fill(0x01));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, movesAfterFirstRefresh, null,
                NullLogger.Instance, cts.Token);

            Assert.True(refreshes >= 2, "the pivot never moved during the heal, so the test proves nothing");
            Assert.Equal(nearbyHeader.StateRoot, rollingPivot.Current.Header.StateRoot);
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
            Assert.Equal(served.Root, bundle.ReconcileRoots.Last());
        }

        [Fact]
        public async Task Given_AStoredRootWhoseCertifyReportsAHoleAndAPivotThatMovesDuringTheRepairHealWithoutARotation_When_TheRepairHealMatches_Then_TheRetriedCertifyRunsAtTheRootTheHealConvergedTo()
        {
            var served = new ServedContract();
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: 1);
            served.StoreIn(bundle);
            served.Scheduler.FirstResponseDelay = TimeSpan.FromSeconds(13);
            var servedHeader = Header(600, served.Root);
            var nearbyHeader = Header(610, Fill(0xEE));
            var refreshes = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> movesAfterFirstRefresh = (_, _) =>
                Task.FromResult<(BlockHeader, byte[])?>(Interlocked.Increment(ref refreshes) == 1
                    ? (servedHeader, Fill(0x02))
                    : (nearbyHeader, Fill(0x03)));
            var rollingPivot = new SnapBootstrapper.RollingPivot(servedHeader, Fill(0x02));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, movesAfterFirstRefresh, null,
                NullLogger.Instance, cts.Token);

            Assert.True(refreshes >= 2, "the pivot never moved during the repair heal, so the test proves nothing");
            Assert.Equal(nearbyHeader.StateRoot, rollingPivot.Current.Header.StateRoot);
            Assert.NotEmpty(served.Scheduler.TrieNodeRoots);
            Assert.Equal(new[] { served.Root, served.Root }, bundle.ReconcileRoots.ToArray());
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
        }

        [Fact]
        public async Task Given_PeerRepairsThatNeverConverge_When_Finalizing_Then_ItSurfacesAfterFourAttempts()
        {
            var served = new ServedContract();
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: int.MaxValue);
            var servedHeader = Header(600, served.Root);
            var rollingPivot = new SnapBootstrapper.RollingPivot(servedHeader, Fill(0x02));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(() => SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, Answers(servedHeader, 0x02), null,
                NullLogger.Instance, cts.Token));

            Assert.Equal(4, bundle.ReconcileCalls);
        }

        [Fact]
        public async Task Given_EveryPeerStatelessAtTheRepairRoot_When_TheFlatRepairHealMakesNoProgress_Then_ItWaitsForANewPivotInsteadOfSurfacing()
        {
            var served = new ServedContract();
            var bundle = new ScriptedReconcileBundle(served.Holes, unresolvableCalls: 1);
            var unservedHeader = Header(100, Fill(0xCD));
            var servedHeader = Header(600, served.Root);
            var rollingPivot = new SnapBootstrapper.RollingPivot(unservedHeader, Fill(0x01));
            var log = new MessageLog();
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> advancesOnlyOnceWaiting = (_, _) =>
                Task.FromResult<(BlockHeader, byte[])?>(log.Count(WaitingForPivot) == 0
                    ? (unservedHeader, Fill(0x01))
                    : (servedHeader, Fill(0x02)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, served.Scheduler, rollingPivot, advancesOnlyOnceWaiting, null,
                log, cts.Token);

            Assert.True(served.Scheduler.StaleTrieNodeCalls > 0, "the repair heal never asked for the unserved root");
            Assert.True(log.Count(WaitingForPivot) >= 1, "the repair heal never waited");
            Assert.Equal(served.Root, finalPivot.Header.StateRoot);
            Assert.Equal(2, bundle.ReconcileCalls);
            Assert.True(served.Scheduler.TargetRootChanges >= 1, "the pivot advanced during the wait but the scheduler's per-root state was not reset");
        }

        private const string WaitingForPivot = "snap.phase3.waiting_for_pivot";
        private static readonly TimeSpan WaitStartBound = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PivotPollInterval = TimeSpan.FromSeconds(12);
        private static readonly TimeSpan PivotPollCap = TimeSpan.FromSeconds(30);

        private sealed class MessageLog : Microsoft.Extensions.Logging.ILogger
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();
            public IReadOnlyCollection<string> Messages => _messages;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _messages.Enqueue(formatter(state, exception));
            public int Count(string fragment) => _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));

            public async Task<bool> WaitForAsync(string fragment, int count, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (Count(fragment) >= count) return true;
                    await Task.Delay(50);
                }
                return Count(fragment) >= count;
            }
        }

        private sealed class UnservedRepair
        {
            public readonly ScriptedReconcileBundle Bundle;
            public readonly WindowedScheduler Scheduler;
            public readonly SnapBootstrapper.RollingPivot RollingPivot;
            public readonly BlockHeader UnservedHeader;
            public readonly BlockHeader ServedHeader;

            public UnservedRepair()
            {
                var serverStore = new InMemoryContentNodeStore();
                var servedTrie = new PatriciaTrie(serverStore, Hp);
                servedTrie.SaveDirtyNodesToStorage();
                var servedRoot = servedTrie.Root.GetHash();
                Scheduler = new WindowedScheduler(new PatriciaSnapRequestHandler(serverStore, new NullBytecodes()), servableRoot: servedRoot);
                Bundle = new ScriptedReconcileBundle(new[] { (Keccak.CalculateHash(new byte[] { 0x01, 0xAA }), Fill(0xAB)) }, unresolvableCalls: 1);
                UnservedHeader = Header(100, Fill(0xCD));
                ServedHeader = Header(600, servedRoot);
                RollingPivot = new SnapBootstrapper.RollingPivot(UnservedHeader, Fill(0x01));
            }

            public Task<SnapBootstrapper.PivotState> RepairAsync(
                Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
                MessageLog log, CancellationToken ct)
                => SnapBootstrapper.ReconcileFlatStateAsync(
                    Bundle, Scheduler, RollingPivot, pivotRefresher, null,
                    log, ct);
        }

        private static async Task StopAsync(Task run, CancellationTokenSource cts)
        {
            if (run.IsCompleted) return;
            cts.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
        }

        [Fact]
        public async Task Given_AFlatRepairHealAgainstARootNoPeerServes_When_ThePivotDoesNotAdvance_Then_ItWaitsForANewPivotInsteadOfRecycling()
        {
            var damage = new UnservedRepair();
            var log = new MessageLog();
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> samePivot =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((damage.UnservedHeader, Fill(0x01)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = damage.RepairAsync(samePivot, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAsync(WaitingForPivot, 1, WaitStartBound),
                    $"the flat-repair heal never waited; it recycled {log.Count("snap.phase3.recycle")} time(s) against a root no peer serves");
                var callsWhenWaiting = damage.Scheduler.StaleTrieNodeCalls;
                Assert.True(callsWhenWaiting > 0, "the heal never asked for the unserved root");

                await Task.Delay(PivotPollInterval + PivotPollInterval);

                Assert.False(run.IsCompleted, "the repair ended although nothing could have unblocked it");
                Assert.Equal(callsWhenWaiting, damage.Scheduler.StaleTrieNodeCalls);
                Assert.Equal(0, log.Count("snap.phase3.recycle"));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_APivotRefresherThatNeverAnswers_When_AHealCycleEndsWithoutProgress_Then_TheLoopStillReachesTheWait()
        {
            var damage = new UnservedRepair();
            var log = new MessageLog();
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> neverAnswers = async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return null;
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var run = damage.RepairAsync(neverAnswers, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAsync(WaitingForPivot, 1, PivotPollCap + PivotPollCap + PivotPollCap + PivotPollCap),
                    "a pivot refresh that never answers blocked the loop before it could wait");
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_AHealWaitingForAnAdvancedPivot_When_TheRunIsCancelled_Then_ItStopsWithinOnePollInterval()
        {
            var damage = new UnservedRepair();
            var log = new MessageLog();
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> samePivot =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((damage.UnservedHeader, Fill(0x01)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = damage.RepairAsync(samePivot, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAsync(WaitingForPivot, 1, WaitStartBound), "the heal never started waiting, so cancelling it proves nothing");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                cts.Cancel();
                var stopped = await Task.WhenAny(run, Task.Delay(PivotPollInterval + PivotPollCap)) == run;

                Assert.True(stopped, "the waiting heal ignored cancellation");
                Assert.True(sw.Elapsed < PivotPollInterval, $"the waiting heal took {sw.Elapsed} to observe cancellation, longer than one poll interval");
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_AHealWaitingForAnAdvancedPivot_When_APivotPollNeverReturns_Then_ThePollIsAbandonedAtItsCapAndTheNextPollAdoptsTheAdvancedPivot()
        {
            var damage = new UnservedRepair();
            var log = new MessageLog();
            var pollsWhileWaiting = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher = (_, _) =>
            {
                if (log.Count(WaitingForPivot) == 0)
                    return Task.FromResult<(BlockHeader, byte[])?>((damage.UnservedHeader, Fill(0x01)));
                if (Interlocked.Increment(ref pollsWhileWaiting) == 1)
                    return new TaskCompletionSource<(BlockHeader, byte[])?>().Task;
                return Task.FromResult<(BlockHeader, byte[])?>((damage.ServedHeader, Fill(0x02)));
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = damage.RepairAsync(refresher, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAsync(WaitingForPivot, 1, WaitStartBound), "the heal never started waiting");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var finished = await Task.WhenAny(run, Task.Delay(PivotPollInterval + PivotPollCap + PivotPollInterval + TimeSpan.FromSeconds(20))) == run;

                Assert.True(finished, $"the wait stayed frozen on a pivot poll that never returned ({pollsWhileWaiting} poll(s) while waiting)");
                var finalPivot = await run;
                Assert.True(pollsWhileWaiting >= 2, "the advanced pivot was adopted without the hung poll being abandoned");
                Assert.True(sw.Elapsed >= PivotPollCap, $"the hung poll was abandoned after {sw.Elapsed}, before its cap");
                Assert.Equal(damage.ServedHeader.StateRoot, finalPivot.Header.StateRoot);
                Assert.Equal(2, damage.Bundle.ReconcileCalls);
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }
    }
}
