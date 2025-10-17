using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4.Pools
{
    public interface IPoolCacheRepository
    {
        Task<PoolCacheEntry> GetPoolAsync(string poolId);
        Task SavePoolAsync(PoolCacheEntry entry);
        Task<List<PoolCacheEntry>> GetAllPoolsAsync();
        Task ClearAsync();
    }
}





