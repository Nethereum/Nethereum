using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public interface ITokenListDiffStorage
    {
        Task<List<TokenInfo>> GetAdditionalTokensAsync(long chainId);
        Task SaveAdditionalTokensAsync(long chainId, List<TokenInfo> tokens);
        Task<DateTime?> GetLastUpdateAsync(long chainId);
        Task SetLastUpdateAsync(long chainId, DateTime updateTime);
        Task ClearAsync(long chainId);
    }
}
