using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.Eth.DTOs;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "IUserOpMempool - the pending-UserOperation pool contract")]
    public interface IUserOpMempool
    {
        Task<MempoolAddOutcome> AddAsync(MempoolEntry entry);

        Task<MempoolEntry?> GetAsync(string userOpHash);

        Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null);

        Task<MempoolEntry[]> GetAllPendingAsync();

        Task<MempoolEntry[]> GetBySenderAsync(string sender);

        Task<bool> RemoveAsync(string userOpHash);

        Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash);

        Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber, string? blockHash = null);

        Task MarkFailedAsync(string[] userOpHashes, string error);

        Task RevertSubmittedAsync(string transactionHash);

        Task ClearAsync();

        Task<int> CountAsync();

        Task<MempoolStats> GetStatsAsync();

        Task<int> PruneAsync();
    }

    public class MempoolEntry
    {
        public string UserOpHash { get; set; } = null!;

        public PackedUserOperation UserOperation { get; set; } = null!;

        public string EntryPoint { get; set; } = null!;

        public DateTimeOffset SubmittedAt { get; set; }

        public MempoolEntryState State { get; set; } = MempoolEntryState.Pending;

        public string? TransactionHash { get; set; }

        public BigInteger? BlockNumber { get; set; }

        public string? BlockHash { get; set; }

        public string? Error { get; set; }

        public int RetryCount { get; set; }

        public BigInteger Prefund { get; set; }

        public string? Aggregator { get; set; }

        public ulong? ValidUntil { get; set; }

        public ulong? ValidAfter { get; set; }

        public string? Factory { get; set; }

        public string? Paymaster { get; set; }

        public BigInteger Priority { get; set; }

        public HashSet<string> AccessedStorageAddresses { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Authorisation? Eip7702Auth { get; set; }
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "MempoolAddOutcome - added, replaced, or which rejection")]
    public enum MempoolAddOutcome
    {
        Added,

        Replaced,

        RejectedDuplicate,

        RejectedUnderpriced,

        RejectedFull
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "MempoolEntryState - the lifecycle of a pooled operation")]
    public enum MempoolEntryState
    {
        Pending,
        Submitted,
        Included,
        Failed,
        Dropped
    }

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
