using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        private static async Task<object> InvokeReconcileFlatStateAsync(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            SnapBootstrapper.RollingPivot rollingPivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher,
            CancellationToken ct)
        {
            var method = typeof(SnapBootstrapper).GetMethod(
                "ReconcileFlatStateAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(null, new object[]
            {
                bundle, scheduler, rollingPivot, pivotRefresher, null,
                true,
                true,
                NullLogger.Instance, ct,
            })!;
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }

        private sealed class CountingHealNodeSink : IHealNodeSink
        {
            private readonly IHealNodeSink _inner;
            public int ForceWipeCalls;
            public int WipeCalls;
            public CountingHealNodeSink(IHealNodeSink inner) => _inner = inner;

            public bool HasNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
                => _inner.HasNode(isStorage, accountHash, nibblePath, expectedHash);
            public HealNodePresence Probe(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
                => _inner.Probe(isStorage, accountHash, nibblePath, expectedHash);
            public void PutNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash, byte[] blob)
                => _inner.PutNode(isStorage, accountHash, nibblePath, expectedHash, blob);
            public void WipeStorage(byte[] accountHash)
            { Interlocked.Increment(ref WipeCalls); _inner.WipeStorage(accountHash); }
            public void ForceWipeStorage(byte[] accountHash)
            { Interlocked.Increment(ref ForceWipeCalls); _inner.ForceWipeStorage(accountHash); }
            public bool HasRoot(byte[] root) => _inner.HasRoot(root);
            public void Flush() => _inner.Flush();
        }

        private sealed class PendingDamageBundle : IChainStoreBundle, IFlatStateReconciler, IHealNodeSinkProvider
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
            private List<(byte[] AccountHash, byte[] StorageRoot)> _damage;
            public readonly CountingHealNodeSink Sink;

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
                => Task.FromResult(EmptyReconcileResult());
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

        private sealed class NoFlatWriterStateStore : IStateStore
        {
            private readonly IStateStore _inner;
            public NoFlatWriterStateStore(IStateStore inner) => _inner = inner;

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, System.Numerics.BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, System.Numerics.BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<System.Numerics.BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
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

            public WindowedScheduler(PatriciaSnapRequestHandler handler, byte[] servableRoot)
            {
                _handler = handler;
                _servableRoot = servableRoot;
            }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
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

        [Fact]
        public async Task Given_AllPendingDamageSeedsAbsentAtFreshRoot_When_Reconciling_Then_DamageClearsAndNoThrow_NotWedged()
        {
            var serverStore = new InMemoryContentNodeStore();
            var freshStateTrie = new PatriciaTrie(serverStore, Hp);
            freshStateTrie.SaveDirtyNodesToStorage();
            var freshRoot = freshStateTrie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: freshRoot);

            var damagedAccount = Keccak.CalculateHash(new byte[] { 0x01, 0xAA });
            var damagedStorageRoot = Fill(0xAB);
            var bundle = new PendingDamageBundle(new[] { (damagedAccount, damagedStorageRoot) });

            var staleRoot = Fill(0xCD);
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, staleRoot), Fill(0x01));
            var freshHeader = Header(600, freshRoot);
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher =
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((freshHeader, Fill(0x02)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await InvokeReconcileFlatStateAsync(bundle, scheduler, rollingPivot, pivotRefresher, cts.Token);

            Assert.Empty(bundle.GetPersistedDamage());
            Assert.True(scheduler.StaleTrieNodeCalls > 0, "the pinned stale root must genuinely have been tried first");
            Assert.True(scheduler.AccountRangeCalls >= 1, "the retarget must re-resolve the seed with a proof-verified account fetch");
        }

        [Fact]
        public async Task Given_PendingDamageNeedsRetarget_When_Reentering_Then_ConvergesWithoutReWipingBankedSubtrees()
        {
            var serverStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(serverStore, Hp);
            for (byte i = 1; i <= 3; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { i });
                storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xA0 + i) }));
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountHash = Keccak.CalculateHash(new byte[] { 0x01, 0xAA });
            var account = new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            var accountTrie = new PatriciaTrie(serverStore, Hp);
            accountTrie.Put(accountHash, account);
            accountTrie.SaveDirtyNodesToStorage();
            var freshRoot = accountTrie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: freshRoot);

            var bundle = new PendingDamageBundle(new[] { (accountHash, storageRoot) });

            var staleRoot = Fill(0xCD);
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, staleRoot), Fill(0x01));
            var freshHeader = Header(600, freshRoot);
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher =
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((freshHeader, Fill(0x02)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await InvokeReconcileFlatStateAsync(bundle, scheduler, rollingPivot, pivotRefresher, cts.Token);

            Assert.Empty(bundle.GetPersistedDamage());
            Assert.True(scheduler.StaleTrieNodeCalls > 0, "the pinned stale root must genuinely have been tried first");
            Assert.True(scheduler.AccountRangeCalls >= 1, "the retarget must re-resolve the seed with a proof-verified account fetch");

            Assert.Equal(1, bundle.Sink.ForceWipeCalls);
        }

        private sealed class UnresolvableOnceBundle : IChainStoreBundle, IFlatStateReconciler, IHealNodeSinkProvider
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
            private readonly IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> _seed;
            public readonly CountingHealNodeSink Sink;
            public int ReconcileCalls;

            public UnresolvableOnceBundle(IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seed)
            {
                _seed = seed;
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
                var call = Interlocked.Increment(ref ReconcileCalls);
                if (call == 1)
                    throw new FlatReconcileUnresolvableNodeException(
                        $"{_seed.Count} unresolvable subtree(s) surfaced while reconciling flat state", _seed, null);
                return Task.FromResult(EmptyReconcileResult());
            }
            public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
                => Task.FromResult(EmptyReconcileResult());
            public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage() => Array.Empty<(byte[], byte[])>();
            public void ClearPersistedDamage() { }
            public IReadOnlyList<byte[]> GetPersistedMissingCode() => Array.Empty<byte[]>();
            public void ClearPersistedMissingCode() { }

            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();

            private static FlatStateReconcileResult EmptyReconcileResult() => new(0, 0, 0, 0, 0, 0, 0, 0);
        }

        [Fact]
        public async Task Given_UnresolvableNodeDuringReconcile_When_Reconciling_Then_TheSeededHealLoopRepairsAndReconcileConverges()
        {
            var serverStore = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(serverStore, Hp);
            for (byte i = 1; i <= 3; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { i });
                storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xB0 + i) }));
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountHash = Keccak.CalculateHash(new byte[] { 0x03, 0xCC });
            var account = new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            var accountTrie = new PatriciaTrie(serverStore, Hp);
            accountTrie.Put(accountHash, account);
            accountTrie.SaveDirtyNodesToStorage();
            var freshRoot = accountTrie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: freshRoot);

            var bundle = new UnresolvableOnceBundle(new[] { (accountHash, storageRoot) });

            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(900, freshRoot), Fill(0x07));
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> pivotRefresher =
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>(null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await InvokeReconcileFlatStateAsync(bundle, scheduler, rollingPivot, pivotRefresher, cts.Token);

            Assert.Equal(2, bundle.ReconcileCalls);
            Assert.True(scheduler.ServedTrieNodeCalls >= 1, "the seeded heal loop must actually resolve the unresolvable subtree via the scheduler");
        }
    }
}
