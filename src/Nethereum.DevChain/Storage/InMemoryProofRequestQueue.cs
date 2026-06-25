using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;

namespace Nethereum.DevChain.Storage
{
    public class InMemoryProofRequestQueue : IProofRequestQueue
    {
        private readonly ConcurrentDictionary<long, ProofRequest> _requests = new();

        public Task EnqueueAsync(long blockNumber)
        {
            _requests.AddOrUpdate(blockNumber,
                _ => new ProofRequest { BlockNumber = blockNumber, Status = ProofRequestStatus.Queued },
                (_, existing) =>
                {
                    if (existing.Status == ProofRequestStatus.Failed ||
                        existing.Status == ProofRequestStatus.Completed)
                    {
                        existing.Status = ProofRequestStatus.Queued;
                        existing.LastError = null;
                    }
                    return existing;
                });
            return Task.CompletedTask;
        }

        public Task<ProofRequest?> DequeueAsync()
        {
            var next = _requests.Values
                .Where(r => r.Status == ProofRequestStatus.Queued)
                .OrderBy(r => r.BlockNumber)
                .FirstOrDefault();

            if (next != null)
                next.Status = ProofRequestStatus.Processing;

            return Task.FromResult(next);
        }

        public Task CompleteAsync(long blockNumber)
        {
            if (_requests.TryGetValue(blockNumber, out var req))
                req.Status = ProofRequestStatus.Completed;
            return Task.CompletedTask;
        }

        public Task FailAsync(long blockNumber, string error)
        {
            if (_requests.TryGetValue(blockNumber, out var req))
            {
                req.Attempts++;
                req.LastError = error;
                req.Status = ProofRequestStatus.Failed;
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProofRequest>> GetPendingAsync()
        {
            var pending = _requests.Values
                .Where(r => r.Status == ProofRequestStatus.Queued || r.Status == ProofRequestStatus.Processing)
                .OrderBy(r => r.BlockNumber)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProofRequest>>(pending);
        }

        public Task<ProofRequest?> GetStatusAsync(long blockNumber)
        {
            _requests.TryGetValue(blockNumber, out var req);
            return Task.FromResult(req);
        }
    }
}
