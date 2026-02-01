using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.AccountAbstraction.DTOs;

namespace Nethereum.AccountAbstraction.Bundler
{
    /// <summary>
    /// Core bundler service interface following ERC-4337 bundler RPC specification.
    /// This interface can be implemented both as a client (calling external bundler)
    /// or as a server (running bundler logic locally).
    /// </summary>
    public interface IBundlerService
    {
        /// <summary>
        /// Sends a UserOperation to the bundler for processing.
        /// </summary>
        /// <param name="userOp">The packed user operation</param>
        /// <param name="entryPoint">The EntryPoint contract address</param>
        /// <returns>The user operation hash</returns>
        Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint);

        /// <summary>
        /// Estimates gas values for a UserOperation.
        /// </summary>
        /// <param name="userOp">The user operation to estimate</param>
        /// <param name="entryPoint">The EntryPoint contract address</param>
        /// <returns>Gas estimates for the operation</returns>
        Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp, string entryPoint);

        /// <summary>
        /// Gets the receipt for a UserOperation by its hash.
        /// </summary>
        /// <param name="userOpHash">The user operation hash</param>
        /// <returns>The receipt if the operation has been executed, null otherwise</returns>
        Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash);

        /// <summary>
        /// Gets a UserOperation by its hash.
        /// </summary>
        /// <param name="userOpHash">The user operation hash</param>
        /// <returns>The user operation info if found</returns>
        Task<UserOperationInfo?> GetUserOperationByHashAsync(string userOpHash);

        /// <summary>
        /// Gets the list of supported EntryPoint addresses.
        /// </summary>
        Task<string[]> SupportedEntryPointsAsync();

        /// <summary>
        /// Gets the chain ID the bundler is operating on.
        /// </summary>
        Task<BigInteger> ChainIdAsync();
    }

    /// <summary>
    /// Extended bundler service with additional management methods.
    /// </summary>
    public interface IBundlerServiceExtended : IBundlerService
    {
        /// <summary>
        /// Gets the status of a pending UserOperation.
        /// </summary>
        Task<UserOperationStatus> GetUserOperationStatusAsync(string userOpHash);

        /// <summary>
        /// Gets all pending UserOperations in the mempool.
        /// </summary>
        Task<PendingUserOperation[]> GetPendingUserOperationsAsync();

        /// <summary>
        /// Drops a pending UserOperation from the mempool.
        /// </summary>
        Task<bool> DropUserOperationAsync(string userOpHash);

        /// <summary>
        /// Forces immediate bundle execution.
        /// </summary>
        Task<string?> FlushAsync();

        /// <summary>
        /// Gets bundler statistics.
        /// </summary>
        Task<BundlerStats> GetStatsAsync();

        /// <summary>
        /// Sets the bundler reputation for an entity.
        /// </summary>
        Task SetReputationAsync(string address, ReputationEntry reputation);

        /// <summary>
        /// Gets the reputation for an entity.
        /// </summary>
        Task<ReputationEntry> GetReputationAsync(string address);
    }

    /// <summary>
    /// Information about a user operation including its current state.
    /// </summary>
    public class UserOperationInfo
    {
        public PackedUserOperation UserOperation { get; set; } = null!;
        public string EntryPoint { get; set; } = null!;
        public string UserOpHash { get; set; } = null!;
        public BigInteger BlockNumber { get; set; }
        public string? TransactionHash { get; set; }
    }

    /// <summary>
    /// Status of a pending user operation.
    /// </summary>
    public class UserOperationStatus
    {
        public string UserOpHash { get; set; } = null!;
        public UserOpState State { get; set; }
        public string? TransactionHash { get; set; }
        public string? Error { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
    }

    /// <summary>
    /// State of a user operation in the bundler.
    /// </summary>
    public enum UserOpState
    {
        Pending,
        Submitted,
        Included,
        Failed,
        Dropped
    }

    /// <summary>
    /// A pending user operation in the mempool.
    /// </summary>
    public class PendingUserOperation
    {
        public PackedUserOperation UserOperation { get; set; } = null!;
        public string EntryPoint { get; set; } = null!;
        public string UserOpHash { get; set; } = null!;
        public DateTimeOffset SubmittedAt { get; set; }
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// Bundler statistics.
    /// </summary>
    public class BundlerStats
    {
        public int PendingCount { get; set; }
        public int SubmittedCount { get; set; }
        public int IncludedCount { get; set; }
        public int FailedCount { get; set; }
        public int BundlesSubmitted { get; set; }
        public BigInteger TotalGasUsed { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }

    /// <summary>
    /// Reputation entry for an entity (account, paymaster, factory, aggregator).
    /// </summary>
    public class ReputationEntry
    {
        public string Address { get; set; } = null!;
        public int OpsIncluded { get; set; }
        public int OpsFailed { get; set; }
        public ReputationStatus Status { get; set; }
    }

    /// <summary>
    /// Reputation status levels.
    /// </summary>
    public enum ReputationStatus
    {
        Ok,
        Throttled,
        Banned
    }
}
