using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public interface IAnchorRecordRepository
    {
        Task UpsertAsync(AnchorRecord record);
        Task<AnchorRecord> GetLatestAsync(long chainId);
        Task<AnchorRecord> GetByEndBlockAsync(long chainId, long endBlock);
        Task<IReadOnlyList<AnchorRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50);
        Task<int> GetCountByChainAsync(long chainId);
    }

    public interface IChainRegistrationRepository
    {
        Task UpsertAsync(ChainRegistration registration);
        Task<ChainRegistration?> GetByChainIdAsync(long chainId);
        Task<IReadOnlyList<ChainRegistration>> GetAllAsync();
    }

    public interface IBlockProofRecordRepository
    {
        Task UpsertAsync(BlockProofRecord record);
        Task<IReadOnlyList<BlockProofRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50);
        Task<bool> IsBlockProvenAsync(long chainId, long blockNumber);
    }

    public interface IChainAnchoringSummaryRepository
    {
        Task UpsertAsync(ChainAnchoringSummary summary);
        Task<ChainAnchoringSummary?> GetAsync(long chainId);
        Task<IReadOnlyList<ChainAnchoringSummary>> GetAllAsync();
    }
}
