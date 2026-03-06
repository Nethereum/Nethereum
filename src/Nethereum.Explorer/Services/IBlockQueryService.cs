using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface IBlockQueryService
{
    Task<IBlockView?> GetBlockAsync(long blockNumber);
    Task<IBlockView?> GetLatestBlockAsync();
    Task<List<IBlockView>> GetLatestBlocksAsync(int count);
    Task<List<IBlockView>> GetBlocksPagedAsync(int page, int pageSize);
    Task<IBlockView?> GetBlockByHashAsync(string hash);
    Task<int> GetTotalBlockCountAsync();
}
