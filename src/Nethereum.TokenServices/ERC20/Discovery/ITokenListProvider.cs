using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public interface ITokenListProvider
    {
        Task<List<TokenInfo>> GetTokensAsync(long chainId);
        Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress);
        Task<bool> SupportsChainAsync(long chainId);
    }
}
