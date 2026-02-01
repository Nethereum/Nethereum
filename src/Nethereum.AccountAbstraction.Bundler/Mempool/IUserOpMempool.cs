using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    /// <summary>
    /// Interface for UserOperation mempool management.
    /// </summary>
    public interface IUserOpMempool
    {
        /// <summary>
        /// Adds a UserOperation to the mempool.
        /// </summary>
        /// <param name="entry">The mempool entry to add</param>
        /// <returns>True if added successfully, false if rejected (duplicate, full, etc.)</returns>
        Task<bool> AddAsync(MempoolEntry entry);

        /// <summary>
        /// Gets a UserOperation by its hash.
        /// </summary>
        Task<MempoolEntry?> GetAsync(string userOpHash);

        /// <summary>
        /// Gets pending UserOperations for bundling, ordered by priority.
        /// </summary>
        /// <param name="maxCount">Maximum number of operations to return</param>
        /// <param name="maxGas">Maximum total gas for the batch</param>
        Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null);

        /// <summary>
        /// Gets all pending UserOperations for a sender.
        /// </summary>
        Task<MempoolEntry[]> GetBySenderAsync(string sender);

        /// <summary>
        /// Removes a UserOperation from the mempool.
        /// </summary>
        Task<bool> RemoveAsync(string userOpHash);

        /// <summary>
        /// Marks UserOperations as submitted in a transaction.
        /// </summary>
        Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash);

        /// <summary>
        /// Marks UserOperations as included (confirmed on-chain).
        /// </summary>
        Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber);

        /// <summary>
        /// Marks UserOperations as failed.
        /// </summary>
        Task MarkFailedAsync(string[] userOpHashes, string error);

        /// <summary>
        /// Returns submitted but not yet confirmed operations to pending.
        /// Called when a bundle transaction is dropped or reorged.
        /// </summary>
        Task RevertSubmittedAsync(string transactionHash);

        /// <summary>
        /// Clears all entries from the mempool.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Gets the current mempool size.
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// Gets mempool statistics.
        /// </summary>
        Task<MempoolStats> GetStatsAsync();

        /// <summary>
        /// Removes expired or stale entries.
        /// </summary>
        Task<int> PruneAsync();
    }

    /// <summary>
    /// A mempool entry containing the UserOperation and metadata.
    /// </summary>
    public class MempoolEntry
    {
        /// <summary>
        /// Unique hash of the UserOperation.
        /// </summary>
        public string UserOpHash { get; set; } = null!;

        /// <summary>
        /// The packed UserOperation.
        /// </summary>
        public PackedUserOperation UserOperation { get; set; } = null!;

        /// <summary>
        /// The EntryPoint address.
        /// </summary>
        public string EntryPoint { get; set; } = null!;

        /// <summary>
        /// When the operation was added to the mempool.
        /// </summary>
        public DateTimeOffset SubmittedAt { get; set; }

        /// <summary>
        /// Current state of the operation.
        /// </summary>
        public MempoolEntryState State { get; set; } = MempoolEntryState.Pending;

        /// <summary>
        /// Transaction hash if submitted.
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Block number if included.
        /// </summary>
        public BigInteger? BlockNumber { get; set; }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Number of times this operation has been retried.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Prefund required for this operation.
        /// </summary>
        public BigInteger Prefund { get; set; }

        /// <summary>
        /// Aggregator address if using signature aggregation.
        /// </summary>
        public string? Aggregator { get; set; }

        /// <summary>
        /// Validation deadline (validUntil from validation data).
        /// </summary>
        public ulong? ValidUntil { get; set; }

        /// <summary>
        /// Validation start time (validAfter from validation data).
        /// </summary>
        public ulong? ValidAfter { get; set; }

        /// <summary>
        /// Factory address if this creates a new account.
        /// </summary>
        public string? Factory { get; set; }

        /// <summary>
        /// Paymaster address if using a paymaster.
        /// </summary>
        public string? Paymaster { get; set; }

        /// <summary>
        /// Priority score for ordering (higher = more priority).
        /// Typically based on max priority fee.
        /// </summary>
        public BigInteger Priority { get; set; }
    }

    /// <summary>
    /// State of a mempool entry.
    /// </summary>
    public enum MempoolEntryState
    {
        Pending,
        Submitted,
        Included,
        Failed,
        Dropped
    }

    /// <summary>
    /// Mempool statistics.
    /// </summary>
    public class MempoolStats
    {
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int SubmittedCount { get; set; }
        public int IncludedCount { get; set; }
        public int FailedCount { get; set; }
        public int UniqueSenders { get; set; }
        public int UniquePaymasters { get; set; }
        public BigInteger TotalPrefund { get; set; }
    }
}
