using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface IContractQueryService
{
    Task<List<IContractView>> GetContractsPagedAsync(int page, int pageSize);
    Task<int> GetTotalContractCountAsync();
}
