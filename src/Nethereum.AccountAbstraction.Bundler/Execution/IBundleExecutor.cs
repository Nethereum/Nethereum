using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    /// <summary>
    /// Interface for building and executing bundles.
    /// </summary>
    public interface IBundleExecutor
    {
        /// <summary>
        /// Builds a bundle from mempool entries.
        /// </summary>
        /// <param name="entries">The mempool entries to include</param>
        /// <returns>A bundle ready for execution</returns>
        Task<Bundle> BuildBundleAsync(MempoolEntry[] entries);

        /// <summary>
        /// Executes a bundle by submitting to the chain.
        /// </summary>
        /// <param name="bundle">The bundle to execute</param>
        /// <returns>The transaction receipt</returns>
        Task<BundleExecutionResult> ExecuteAsync(Bundle bundle);

        /// <summary>
        /// Estimates gas for a bundle.
        /// </summary>
        Task<BigInteger> EstimateBundleGasAsync(Bundle bundle);
    }

    /// <summary>
    /// A bundle of UserOperations ready for submission.
    /// </summary>
    public class Bundle
    {
        /// <summary>
        /// The mempool entries included in this bundle.
        /// </summary>
        public MempoolEntry[] Entries { get; set; } = Array.Empty<MempoolEntry>();

        /// <summary>
        /// The EntryPoint address for this bundle.
        /// </summary>
        public string EntryPoint { get; set; } = null!;

        /// <summary>
        /// The beneficiary address for fees.
        /// </summary>
        public string Beneficiary { get; set; } = null!;

        /// <summary>
        /// Estimated total gas for the bundle.
        /// </summary>
        public BigInteger EstimatedGas { get; set; }

        /// <summary>
        /// When the bundle was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// User operation hashes in this bundle.
        /// </summary>
        public string[] UserOpHashes => Entries.Select(e => e.UserOpHash).ToArray();

        /// <summary>
        /// Operations grouped by aggregator. Empty if no aggregation is used.
        /// Key is the aggregator address, value contains the entries and aggregated signature.
        /// </summary>
        public Dictionary<string, AggregatedGroup> AggregatedGroups { get; set; } = new();

        /// <summary>
        /// Whether this bundle uses aggregation.
        /// </summary>
        public bool UsesAggregation => AggregatedGroups.Count > 0;

        /// <summary>
        /// Entries that don't use any aggregator.
        /// </summary>
        public MempoolEntry[] NonAggregatedEntries =>
            Entries.Where(e => !AggregatedGroups.Values.Any(g => g.Entries.Contains(e))).ToArray();
    }

    /// <summary>
    /// A group of UserOperations that share the same aggregator.
    /// </summary>
    public class AggregatedGroup
    {
        /// <summary>
        /// The aggregator address.
        /// </summary>
        public string Aggregator { get; set; } = null!;

        /// <summary>
        /// The entries in this group.
        /// </summary>
        public MempoolEntry[] Entries { get; set; } = Array.Empty<MempoolEntry>();

        /// <summary>
        /// The aggregated signature for all operations in this group.
        /// </summary>
        public byte[] AggregatedSignature { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Result of bundle execution.
    /// </summary>
    public class BundleExecutionResult
    {
        /// <summary>
        /// Whether the bundle was successfully submitted.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The transaction hash.
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// The transaction receipt (if waited for).
        /// </summary>
        public TransactionReceipt? Receipt { get; set; }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Results for individual UserOperations.
        /// </summary>
        public UserOpExecutionResult[] UserOpResults { get; set; } = Array.Empty<UserOpExecutionResult>();

        /// <summary>
        /// Total gas used by the bundle.
        /// </summary>
        public BigInteger GasUsed { get; set; }

        public static BundleExecutionResult Failed(string error) => new() { Success = false, Error = error };

        public static BundleExecutionResult Succeeded(string txHash, TransactionReceipt receipt) => new()
        {
            Success = true,
            TransactionHash = txHash,
            Receipt = receipt,
            GasUsed = receipt.GasUsed?.Value ?? 0
        };
    }

    /// <summary>
    /// Result for an individual UserOperation in a bundle.
    /// </summary>
    public class UserOpExecutionResult
    {
        public string UserOpHash { get; set; } = null!;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public BigInteger ActualGasUsed { get; set; }
        public BigInteger ActualGasCost { get; set; }
    }
}
