using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Proving
{
    public class ProofRequest
    {
        public long BlockNumber { get; set; }
        public ProofRequestStatus Status { get; set; } = ProofRequestStatus.Queued;
        public int Attempts { get; set; }
        public string? LastError { get; set; }
    }

    public enum ProofRequestStatus
    {
        Queued,
        Processing,
        Completed,
        Failed
    }

    public interface IProofRequestQueue
    {
        Task EnqueueAsync(long blockNumber);
        Task<ProofRequest?> DequeueAsync();
        Task CompleteAsync(long blockNumber);
        Task FailAsync(long blockNumber, string error);
        Task<IReadOnlyList<ProofRequest>> GetPendingAsync();
        Task<ProofRequest?> GetStatusAsync(long blockNumber);
    }
}
