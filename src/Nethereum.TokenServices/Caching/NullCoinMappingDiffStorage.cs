using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Pricing;

namespace Nethereum.TokenServices.Caching
{
    public class NullCoinMappingDiffStorage : ICoinMappingDiffStorage
    {
        public Task<Dictionary<string, string>> GetAdditionalMappingsAsync(long chainId)
            => Task.FromResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public Task SaveAdditionalMappingsAsync(long chainId, Dictionary<string, string> mappings)
            => Task.CompletedTask;

        public Task<Dictionary<string, string>> GetAndUpdateMappingsAsync(
            long chainId,
            Func<Dictionary<string, string>, Dictionary<string, string>> updateFunc)
        {
            var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(updateFunc(empty));
        }

        public Task<DateTime?> GetLastUpdateAsync(long chainId)
            => Task.FromResult<DateTime?>(null);

        public Task SetLastUpdateAsync(long chainId, DateTime updateTime)
            => Task.CompletedTask;

        public Task ClearAsync(long chainId)
            => Task.CompletedTask;
    }
}
