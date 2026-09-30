using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler
{
    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "IBundlerService - the ERC-4337 spec RPC surface")]
    public interface IBundlerService
    {
        Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null);

        Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp, string entryPoint);

        Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash);

        Task<IncludedUserOperation?> GetUserOperationByHashAsync(string userOpHash);

        Task<string[]> SupportedEntryPointsAsync();

        Task<BigInteger> ChainIdAsync();
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "IBundlerServiceExtended - debug/ops methods beyond the spec surface")]
    public interface IBundlerServiceExtended : IBundlerService
    {
        Task<UserOperationStatus> GetUserOperationStatusAsync(string userOpHash);

        Task<PendingUserOperation[]> GetPendingUserOperationsAsync();

        Task<bool> DropUserOperationAsync(string userOpHash);

        Task<string?> FlushAsync();

        Task<BundlerStats> GetStatsAsync();

        Task SetReputationAsync(string address, ReputationEntry reputation);

        Task<ReputationEntry> GetReputationAsync(string address);

        Task<ReputationEntry[]> GetAllReputationAsync();

        Task<StakeStatus> GetStakeStatusAsync(string address, string entryPoint);

        void SetBundlingMode(BundlingMode mode);

        Task ClearStateAsync();

        Task ClearMempoolAsync();

        Task ClearReputationAsync();
    }

    public enum BundlingMode
    {
        Auto,
        Manual
    }

    public class IncludedUserOperation
    {
        public PackedUserOperation UserOperation { get; set; } = null!;
        public string EntryPoint { get; set; } = null!;
        public string UserOpHash { get; set; } = null!;
        public BigInteger BlockNumber { get; set; }
        public string? TransactionHash { get; set; }
        public string? BlockHash { get; set; }
    }

    public class UserOperationStatus
    {
        public string UserOpHash { get; set; } = null!;
        public UserOpState State { get; set; }
        public string? TransactionHash { get; set; }
        public string? Error { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
    }

    public enum UserOpState
    {
        Pending,
        Submitted,
        Included,
        Failed,
        Dropped
    }

    public class PendingUserOperation
    {
        public PackedUserOperation UserOperation { get; set; } = null!;
        public string EntryPoint { get; set; } = null!;
        public string UserOpHash { get; set; } = null!;
        public DateTimeOffset SubmittedAt { get; set; }
        public int RetryCount { get; set; }
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "BundlerStats - the bundler's running counters")]
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

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "ReputationEntry - per-entity reputation counters and ban/throttle state")]
    public class ReputationEntry
    {
        public string Address { get; set; } = null!;

        public int OpsSeen { get; set; }
        public int OpsIncluded { get; set; }
        public int OpsFailed { get; set; }
        public int OpsDropped { get; set; }
        public ReputationStatus Status { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public DateTimeOffset? BannedUntil { get; set; }
        public DateTimeOffset? ThrottledUntil { get; set; }
    }

    public class StakeStatus
    {
        public string Address { get; set; } = null!;
        public BigInteger Stake { get; set; }
        public ulong UnstakeDelaySec { get; set; }
        public bool IsStaked { get; set; }
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "ReputationStatus - Ok, Throttled or Banned")]
    public enum ReputationStatus
    {
        Ok,
        Throttled,
        Banned
    }
}
