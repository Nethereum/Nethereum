using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    internal sealed class CountingHealNodeSink : IHealNodeSink
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
        public void DeleteNode(bool isStorage, byte[] accountHash, byte[] nibblePath)
            => _inner.DeleteNode(isStorage, accountHash, nibblePath);
        public void WipeStorage(byte[] accountHash)
        { Interlocked.Increment(ref WipeCalls); _inner.WipeStorage(accountHash); }
        public void ForceWipeStorage(byte[] accountHash)
        { Interlocked.Increment(ref ForceWipeCalls); _inner.ForceWipeStorage(accountHash); }
        public bool HasRoot(byte[] root) => _inner.HasRoot(root);
        public void Flush() => _inner.Flush();
    }

    internal sealed class NoFlatWriterStateStore : IStateStore
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

    internal sealed class ScriptedReconcileBundle : IChainStoreBundle, IFlatStateReconciler, IHealNodeSinkProvider
    {
        private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
        private readonly IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> _holes;
        private readonly int _unresolvableCalls;
        public readonly CountingHealNodeSink Sink;
        public readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> ReconcileRoots = new();
        public int ReconcileCalls;
        public Action<int> OnReconcile;

        private IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> _persistedDamage;
        public int ClearPersistedDamageCalls;

        public ScriptedReconcileBundle(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> holes,
            int unresolvableCalls,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> persistedDamage = null)
        {
            _holes = holes;
            _persistedDamage = persistedDamage ?? Array.Empty<(byte[], byte[])>();
            _unresolvableCalls = unresolvableCalls;
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
            var call = Interlocked.Increment(ref ReconcileCalls);
            OnReconcile?.Invoke(call);
            if (call <= _unresolvableCalls)
                throw new FlatReconcileUnresolvableNodeException(
                    $"{_holes.Count} unresolvable subtree(s) surfaced while reconciling flat state", _holes, null);
            return Task.FromResult(new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0));
        }
        public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            => Task.FromResult(new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0));
        public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage() => _persistedDamage;
        public void ClearPersistedDamage()
        {
            ClearPersistedDamageCalls++;
            _persistedDamage = Array.Empty<(byte[], byte[])>();
        }
        public List<byte[]> MissingCode { get; } = new();
        public IReadOnlyList<byte[]> GetPersistedMissingCode() => MissingCode.ToList();
        public void ClearPersistedMissingCode() => MissingCode.Clear();

        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
