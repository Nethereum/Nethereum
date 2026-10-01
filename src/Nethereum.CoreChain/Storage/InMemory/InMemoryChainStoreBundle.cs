using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public sealed class InMemoryChainStoreBundle : IChainStoreBundle
    {
        public IStateStore         State        { get; }
        public ITrieNodeStore      TrieNodes    { get; }
        public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes => TrieNodes;
        public NodeCommitBlockContext NodeCommitBlockSource => null;
        public IBlockStore         Blocks       { get; }
        public ITransactionStore   Transactions { get; }
        public IUncleStore         Uncles       { get; }
        public IWithdrawalStore    Withdrawals  { get; }
        public IBlockAccessListStore BlockAccessLists { get; }
        public IReceiptStore       Receipts     { get; }
        public ILogStore           Logs         { get; }
        public IChainMetadataStore Metadata     { get; }
        public IStateDiffStore     Diffs        { get; }
        public bool                JournalEnabled { get; }

        public long FreezerHead => 0;
        public long ByHashIndexedHead => 0;
        public long LogIndexRenderedHead => 0;
        public long LogRenderProgressBlock => 0;

        private readonly SemaphoreSlim _batchCommitLock = new(1, 1);

        private InMemoryChainStoreBundle(
            IStateStore state, ITrieNodeStore trie, IBlockStore blocks,
            ITransactionStore transactions, IUncleStore uncles,
            IWithdrawalStore withdrawals,
            IBlockAccessListStore blockAccessLists,
            IReceiptStore receipts, ILogStore logs,
            IChainMetadataStore metadata, IStateDiffStore diffs,
            bool journalEnabled)
        {
            State = state;
            TrieNodes = trie;
            Blocks = blocks;
            Transactions = transactions;
            Uncles = uncles;
            Withdrawals = withdrawals;
            BlockAccessLists = blockAccessLists;
            Receipts = receipts;
            Logs = logs;
            Metadata = metadata;
            Diffs = diffs;
            JournalEnabled = journalEnabled;
        }

        public InMemoryChainStoreBundle WithBlocks(IBlockStore blocks) =>
            new InMemoryChainStoreBundle(
                State, TrieNodes, blocks, Transactions, Uncles, Withdrawals, BlockAccessLists,
                Receipts, Logs, Metadata, Diffs, JournalEnabled);

        public static InMemoryChainStoreBundle Open(HistoricalStateOptions journalOptions = null)
        {
            var blocks = new InMemoryBlockStore();
            var diffStore = new InMemoryStateDiffStore();
            IStateStore rawState = new InMemoryStateStore();
            IStateStore wired = new StateLayer().Stores.HistoricalInMemory(rawState, diffStore, journalOptions);
            return new InMemoryChainStoreBundle(
                wired,
                new InMemoryContentNodeStore(),
                blocks,
                new InMemoryTransactionStore(blocks),
                new InMemoryUncleStore(blocks),
                new InMemoryWithdrawalStore(blocks),
                new InMemoryBlockAccessListStore(blocks),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                new InMemoryChainMetadataStore(),
                diffStore,
                journalEnabled: journalOptions != null);
        }

        public string ResolveCheckpointSnapshotPath(ulong blockNumber)
            => string.Empty;

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
                "In-memory bundle has no on-disk snapshot — restore is not available. " +
                "Use the journal (StateRewindService) for in-memory rewind.");

        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
        {
            Metadata.DeleteCheckpoint(blockNumber);
            return Task.CompletedTask;
        }

        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default)
            => throw new NotSupportedException(
                "In-memory bundle has no on-disk database — export is not available.");

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
