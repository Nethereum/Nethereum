using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevChain.Composition
{
    public sealed class DevChainChainStoreBundle : IChainStoreBundle
    {
        public IStateStore State { get; }
        public ITrieNodeStore TrieNodes { get; }
        public ITrieNodeStore StateTrieNodes => TrieNodes;
        public NodeCommitBlockContext NodeCommitBlockSource => null;
        public IBlockStore Blocks { get; }
        public ITransactionStore Transactions { get; }
        public IUncleStore Uncles { get; }
        public IWithdrawalStore Withdrawals { get; }
        public IBlockAccessListStore BlockAccessLists { get; }
        public IReceiptStore Receipts { get; }
        public ILogStore Logs { get; }
        public IChainMetadataStore Metadata { get; }
        public IStateDiffStore Diffs { get; }
        public bool JournalEnabled { get; }

        public long FreezerHead => 0;
        public long ByHashIndexedHead => 0;
        public long LogIndexRenderedHead => 0;
        public long LogRenderProgressBlock => 0;

        private readonly SemaphoreSlim _batchCommitLock = new(1, 1);

        public DevChainChainStoreBundle(
            IStateStore state,
            ITrieNodeStore trieNodes,
            IBlockStore blocks,
            ITransactionStore transactions,
            IBlockAccessListStore blockAccessLists,
            IReceiptStore receipts,
            ILogStore logs)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            TrieNodes = trieNodes ?? throw new ArgumentNullException(nameof(trieNodes));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            BlockAccessLists = blockAccessLists ?? throw new ArgumentNullException(nameof(blockAccessLists));
            Receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            Logs = logs ?? throw new ArgumentNullException(nameof(logs));

            Uncles = new InMemoryUncleStore(blocks);
            Withdrawals = new InMemoryWithdrawalStore(blocks);
            Metadata = new InMemoryChainMetadataStore();

            Diffs = (state as HistoricalStateStore)?.DiffStore
                ?? throw new ArgumentException(
                    "A DevChain state store must be a HistoricalStateStore for a bundle to expose reverse " +
                    "diffs (DevChainStoresExtensions.DevChainSqlite/DevChainInMemory always wire one).",
                    nameof(state));
            JournalEnabled = true;
        }

        public static DevChainChainStoreBundle OpenSqlite(SqliteStorageManager manager)
        {
            var blockStore = new SqliteBlockStore(manager);
            return new DevChainChainStoreBundle(
                new StateLayer().Stores.DevChainSqlite(manager),
                new SqliteTrieNodeStore(manager),
                blockStore,
                new SqliteTransactionStore(manager),
                new SqliteBlockAccessListStore(manager, blockStore),
                new SqliteReceiptStore(manager),
                new SqliteLogStore(manager));
        }

        public static DevChainChainStoreBundle OpenInMemory()
        {
            var blockStore = new InMemoryBlockStore();
            return new DevChainChainStoreBundle(
                new StateLayer().Stores.DevChainInMemory(),
                new InMemoryContentNodeStore(),
                blockStore,
                new InMemoryTransactionStore(blockStore),
                new InMemoryBlockAccessListStore(blockStore),
                new InMemoryReceiptStore(),
                new InMemoryLogStore());
        }

        public string ResolveCheckpointSnapshotPath(ulong blockNumber) => string.Empty;

        public Task<ChainCheckpoint> SaveCheckpointAsync(
            ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
        {
            if (stateRoot is null || stateRoot.Length == 0) throw new ArgumentException("stateRoot required", nameof(stateRoot));
            if (blockHash is null || blockHash.Length == 0) throw new ArgumentException("blockHash required", nameof(blockHash));
            Metadata.SaveCheckpoint(blockNumber, stateRoot, blockHash);
            var cp = Metadata.GetCheckpoint(blockNumber)
                     ?? throw new InvalidOperationException(
                         $"Metadata.SaveCheckpoint at {blockNumber} returned but GetCheckpoint reads back null.");
            return Task.FromResult(cp);
        }

        public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
        {
            var rows = Metadata.ListCheckpointBlockNumbers();
            var result = new List<ChainCheckpoint>(rows.Count);
            foreach (var bn in rows)
            {
                var cp = Metadata.GetCheckpoint(bn);
                if (cp is null) continue;
                result.Add(cp.Value);
            }
            return Task.FromResult<IReadOnlyList<ChainCheckpoint>>(result);
        }

        public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
            => throw new NotSupportedException(
                "DevChain's bundle has no on-disk snapshot to restore from through IChainStoreBundle. " +
                "Use DevChainNode.RevertToSnapshotAsync/SetHeadAsync for DevChain's own rewind.");

        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            Metadata.DeleteCheckpoint(blockNumber);
            return Task.CompletedTask;
        }

        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default)
            => throw new NotSupportedException(
                "DevChain's bundle has no on-disk database to export through IChainStoreBundle.");

        public Task ResetStateOnlyAsync(CancellationToken ct = default)
        {
            Metadata.ResetForStateRebuild();
            return Task.CompletedTask;
        }

        public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default)
        {
            Metadata.ClearSnapSyncState();
            Metadata.ClearDeferredHealAccountsBlob();
            Metadata.ClearDeferredHealCodeBlob();
            Metadata.ClearAllDeferredStorageDebts();
            return Task.CompletedTask;
        }

        public IBundleBatch BeginBatch() => new InMemoryBundleBatch(this, _batchCommitLock);

        public void Dispose() { (State as IDisposable)?.Dispose(); }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
