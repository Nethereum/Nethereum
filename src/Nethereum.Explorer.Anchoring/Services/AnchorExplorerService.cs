using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Postgres.Entities;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;

namespace Nethereum.Explorer.Anchoring.Services
{
    public class AnchorExplorerService : IAnchorExplorerService
    {
        private readonly IAnchorRecordRepository _anchorRepo;
        private readonly IChainRegistrationRepository _chainRepo;
        private readonly IBlockProofRecordRepository _proofRepo;
        private readonly IChainAnchoringSummaryRepository _summaryRepo;

        public AnchorExplorerService(
            IAnchorRecordRepository anchorRepo,
            IChainRegistrationRepository chainRepo,
            IBlockProofRecordRepository proofRepo,
            IChainAnchoringSummaryRepository summaryRepo)
        {
            _anchorRepo = anchorRepo;
            _chainRepo = chainRepo;
            _proofRepo = proofRepo;
            _summaryRepo = summaryRepo;
        }

        public bool IsConfigured => true;

        public Task<IReadOnlyList<ChainAnchoringSummary>> GetAllChainSummariesAsync()
            => _summaryRepo.GetAllAsync();

        public Task<ChainAnchoringSummary> GetChainSummaryAsync(long chainId)
            => _summaryRepo.GetAsync(chainId);

        public async Task<IReadOnlyList<AnchorRecord>> GetAnchorsAsync(long chainId, int page = 1, int pageSize = 25)
        {
            var skip = (page - 1) * pageSize;
            return await _anchorRepo.GetByChainAsync(chainId, skip, pageSize).ConfigureAwait(false);
        }

        public Task<int> GetAnchorCountAsync(long chainId)
            => _anchorRepo.GetCountByChainAsync(chainId);

        public async Task<AnchorRecord> GetAnchorByEndBlockAsync(long chainId, long endBlock)
        {
            return await _anchorRepo.GetByEndBlockAsync(chainId, endBlock).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<BlockProofRecord>> GetProofsAsync(long chainId, int page = 1, int pageSize = 25)
        {
            var skip = (page - 1) * pageSize;
            return await _proofRepo.GetByChainAsync(chainId, skip, pageSize).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<ChainRegistration>> GetAllRegistrationsAsync()
            => _chainRepo.GetAllAsync();
    }
}
