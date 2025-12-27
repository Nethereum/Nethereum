using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public interface ICoinMappingDiffStorage
    {
        Task<Dictionary<string, string>> GetAdditionalMappingsAsync(long chainId);
        Task SaveAdditionalMappingsAsync(long chainId, Dictionary<string, string> mappings);
        Task<Dictionary<string, string>> GetAndUpdateMappingsAsync(long chainId, Func<Dictionary<string, string>, Dictionary<string, string>> updateFunc);
        Task<DateTime?> GetLastUpdateAsync(long chainId);
        Task SetLastUpdateAsync(long chainId, DateTime updateTime);
        Task ClearAsync(long chainId);
    }
}
