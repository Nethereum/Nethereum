using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.Caching
{
    public class NullTokenListDiffStorage : ITokenListDiffStorage
    {
        public Task<List<TokenInfo>> GetAdditionalTokensAsync(long chainId)
            => Task.FromResult(new List<TokenInfo>());

        public Task SaveAdditionalTokensAsync(long chainId, List<TokenInfo> tokens)
            => Task.CompletedTask;

        public Task<DateTime?> GetLastUpdateAsync(long chainId)
            => Task.FromResult<DateTime?>(null);

        public Task SetLastUpdateAsync(long chainId, DateTime updateTime)
            => Task.CompletedTask;

        public Task ClearAsync(long chainId)
            => Task.CompletedTask;
    }
}
