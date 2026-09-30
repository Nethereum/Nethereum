using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.Explorer.Anchoring.Services
{
    public class NullAnchorExplorerService : IAnchorExplorerService
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<ChainAnchoringSummary>> GetAllChainSummariesAsync()
            => Task.FromResult<IReadOnlyList<ChainAnchoringSummary>>(Array.Empty<ChainAnchoringSummary>());

        public Task<ChainAnchoringSummary?> GetChainSummaryAsync(long chainId)
            => Task.FromResult<ChainAnchoringSummary?>(null);

        public Task<IReadOnlyList<AnchorRecord>> GetAnchorsAsync(long chainId, int page = 1, int pageSize = 25)
            => Task.FromResult<IReadOnlyList<AnchorRecord>>(Array.Empty<AnchorRecord>());

        public Task<int> GetAnchorCountAsync(long chainId) => Task.FromResult(0);

        public Task<AnchorRecord?> GetAnchorByEndBlockAsync(long chainId, long endBlock)
            => Task.FromResult<AnchorRecord?>(null);

        public Task<IReadOnlyList<BlockProofRecord>> GetProofsAsync(long chainId, int page = 1, int pageSize = 25)
            => Task.FromResult<IReadOnlyList<BlockProofRecord>>(Array.Empty<BlockProofRecord>());

        public Task<IReadOnlyList<ChainRegistration>> GetAllRegistrationsAsync()
            => Task.FromResult<IReadOnlyList<ChainRegistration>>(Array.Empty<ChainRegistration>());
    }
}
