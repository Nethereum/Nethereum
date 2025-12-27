using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public interface ITokenPriceProvider
    {
        Task<Dictionary<string, TokenPrice>> GetPricesAsync(
            IEnumerable<string> tokenIds,
            string vsCurrency = "usd");

        Task<Dictionary<string, TokenPrice>> GetPricesByContractAsync(
            long chainId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd");

        Task<TokenPrice> GetNativeTokenPriceAsync(
            long chainId,
            string vsCurrency = "usd");

        Task<string> GetTokenIdAsync(long chainId, string contractAddress);

        Task<Dictionary<string, string>> GetTokenIdsAsync(
            long chainId,
            IEnumerable<string> contractAddresses);

        bool SupportsChain(long chainId);
    }
}
