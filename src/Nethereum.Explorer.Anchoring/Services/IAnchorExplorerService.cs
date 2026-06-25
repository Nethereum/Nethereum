using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.Explorer.Anchoring.Services
{
    public interface IAnchorExplorerService
    {
        bool IsConfigured { get; }
        Task<IReadOnlyList<ChainAnchoringSummary>> GetAllChainSummariesAsync();
        Task<ChainAnchoringSummary?> GetChainSummaryAsync(long chainId);
        Task<IReadOnlyList<AnchorRecord>> GetAnchorsAsync(long chainId, int page = 1, int pageSize = 25);
        Task<int> GetAnchorCountAsync(long chainId);
        Task<AnchorRecord?> GetAnchorByEndBlockAsync(long chainId, long endBlock);
        Task<IReadOnlyList<BlockProofRecord>> GetProofsAsync(long chainId, int page = 1, int pageSize = 25);
        Task<IReadOnlyList<ChainRegistration>> GetAllRegistrationsAsync();
    }
}
