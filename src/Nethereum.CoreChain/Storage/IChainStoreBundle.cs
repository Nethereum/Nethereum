using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.Storage
{
    public interface IChainStoreBundle : IAsyncDisposable, IDisposable
    {
        IStateStore         State        { get; }
        ITrieNodeStore      TrieNodes    { get; }

        Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes { get; }

        NodeCommitBlockContext NodeCommitBlockSource { get; }

        IBlockStore         Blocks       { get; }
        ITransactionStore   Transactions { get; }
        IUncleStore         Uncles       { get; }
        IWithdrawalStore    Withdrawals  { get; }

        IBlockAccessListStore BlockAccessLists { get; }

        IReceiptStore       Receipts     { get; }
        ILogStore           Logs         { get; }
        IChainMetadataStore Metadata     { get; }
        IStateDiffStore     Diffs        { get; }

        bool JournalEnabled { get; }

        Task<ChainCheckpoint> SaveCheckpointAsync(
            ulong blockNumber,
            byte[] stateRoot,
            byte[] blockHash,
            CancellationToken ct = default);

        Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(
            CancellationToken ct = default);

        Task RestoreCheckpointAsync(
            ulong blockNumber,
            CancellationToken ct = default);

        Task DeleteCheckpointAsync(
            ulong blockNumber,
            CancellationToken ct = default);

        Task ResetStateOnlyAsync(CancellationToken ct = default);

        Task ResetSnapBootstrapStateAsync(CancellationToken ct = default);

        string ResolveCheckpointSnapshotPath(ulong blockNumber);

        Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default);

        long FreezerHead { get; }

        long ByHashIndexedHead { get; }

        long LogIndexRenderedHead { get; }

        long LogRenderProgressBlock { get; }

        IBundleBatch BeginBatch();
    }
}
