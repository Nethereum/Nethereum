using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    public interface IBundleExecutor
    {
        Task<Bundle> BuildBundleAsync(MempoolEntry[] entries);

        Task<BundleExecutionResult> ExecuteAsync(Bundle bundle);

        Task<string> SubmitAsync(Bundle bundle);

        Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash);

        Task<BigInteger> EstimateBundleGasAsync(Bundle bundle);
    }

    public class BundleSimulationRevertedException : Exception
    {
        public string RevertData { get; }

        public BundleSimulationRevertedException(string revertData, string message, Exception innerException)
            : base(message, innerException)
        {
            RevertData = revertData ?? "0x";
        }
    }

    public class BundleFailedOpException : Exception
    {
        public int OpIndex { get; }
        public string Reason { get; }

        public BundleFailedOpException(int opIndex, string reason, string message) : base(message)
        {
            OpIndex = opIndex;
            Reason = reason ?? string.Empty;
        }
    }

    public class Bundle
    {
        public MempoolEntry[] Entries { get; set; } = Array.Empty<MempoolEntry>();

        public string EntryPoint { get; set; } = null!;

        public string Beneficiary { get; set; } = null!;

        public BigInteger EstimatedGas { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public string[] UserOpHashes => Entries.Select(e => e.UserOpHash).ToArray();

        public Dictionary<string, AggregatedGroup> AggregatedGroups { get; set; } = new();

        public bool UsesAggregation => AggregatedGroups.Count > 0;

        public MempoolEntry[] NonAggregatedEntries =>
            Entries.Where(e => !AggregatedGroups.Values.Any(g => g.Entries.Contains(e))).ToArray();

        public MempoolEntry[] SubmissionOrderedEntries =>
            UsesAggregation
                ? AggregatedGroups.Values.SelectMany(g => g.Entries).Concat(NonAggregatedEntries).ToArray()
                : Entries;
    }

    public class AggregatedGroup
    {
        public string Aggregator { get; set; } = null!;

        public MempoolEntry[] Entries { get; set; } = Array.Empty<MempoolEntry>();

        public byte[] AggregatedSignature { get; set; } = Array.Empty<byte>();
    }

    public class BundleExecutionResult
    {
        public bool Success { get; set; }

        public string? TransactionHash { get; set; }

        public TransactionReceipt? Receipt { get; set; }

        public string? Error { get; set; }

        public UserOpExecutionResult[] UserOpResults { get; set; } = Array.Empty<UserOpExecutionResult>();

        public BigInteger GasUsed { get; set; }

        public int? FailedOpIndex { get; set; }

        public string? FailedOpReason { get; set; }

        public bool ReceiptTimedOut { get; set; }

        public static BundleExecutionResult Failed(string error) => new() { Success = false, Error = error };

        public static BundleExecutionResult Succeeded(string txHash, TransactionReceipt receipt) => new()
        {
            Success = true,
            TransactionHash = txHash,
            Receipt = receipt,
            GasUsed = receipt.GasUsed?.Value ?? 0
        };
    }

    public class UserOpExecutionResult
    {
        public string UserOpHash { get; set; } = null!;

        public bool EventFound { get; set; }

        public bool Success { get; set; }
        public string? Error { get; set; }
        public BigInteger ActualGasUsed { get; set; }
        public BigInteger ActualGasCost { get; set; }
    }
}
