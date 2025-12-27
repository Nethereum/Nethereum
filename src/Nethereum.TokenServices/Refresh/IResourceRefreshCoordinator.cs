using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.Refresh
{
    public interface IResourceRefreshCoordinator
    {
        event Action<RefreshJob> OnJobCompleted;
        event Action<RefreshJob, Exception> OnJobFailed;

        void QueueJob(RefreshJob job);
        void QueueStaleResources(IEnumerable<long> activeChainIds);
        Task EnsureChainResourcesAsync(long chainId);
        Task<bool> TryProcessNextJobAsync();
        int PendingJobCount { get; }
        IReadOnlyList<RefreshJob> GetPendingJobs();
    }
}
