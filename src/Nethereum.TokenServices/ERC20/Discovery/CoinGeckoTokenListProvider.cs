using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class CoinGeckoTokenListProvider : ITokenListProvider
    {
        private readonly CoinGeckoApiService _coinGeckoService;
        private readonly ICacheProvider _cacheProvider;
        private readonly TimeSpan _tokenListCacheExpiry;
        private readonly TimeSpan _platformsCacheExpiry;

        public CoinGeckoTokenListProvider(
            CoinGeckoApiService coinGeckoService = null,
            ICacheProvider cacheProvider = null,
            TimeSpan? tokenListCacheExpiry = null,
            TimeSpan? platformsCacheExpiry = null)
        {
            _coinGeckoService = coinGeckoService ?? new CoinGeckoApiService();
            _cacheProvider = cacheProvider ?? new MemoryCacheProvider();
            _tokenListCacheExpiry = tokenListCacheExpiry ?? TimeSpan.FromDays(7);
            _platformsCacheExpiry = platformsCacheExpiry ?? TimeSpan.FromDays(30);
        }

        public async Task<List<TokenInfo>> GetTokensAsync(long chainId)
        {
            var cacheKey = $"tokenlist:{chainId}";

            var cached = await _cacheProvider.GetAsync<List<TokenInfo>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var geckoTokens = await _coinGeckoService.GetTokensForChainAsync(chainId);
            if (geckoTokens == null || !geckoTokens.Any())
            {
                return new List<TokenInfo>();
            }

            var tokens = geckoTokens.Select(t => new TokenInfo
            {
                Address = t.Address,
                Symbol = t.Symbol,
                Name = t.Name,
                Decimals = t.Decimals,
                LogoUri = t.LogoURI,
                ChainId = chainId
            }).ToList();

            await _cacheProvider.SetAsync(cacheKey, tokens, _tokenListCacheExpiry);

            return tokens;
        }

        public async Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            var tokens = await GetTokensAsync(chainId);
            return tokens.FirstOrDefault(t =>
                string.Equals(t.Address, contractAddress, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> SupportsChainAsync(long chainId)
        {
            var cacheKey = $"platform:{chainId}";

            if (await _cacheProvider.ExistsAsync(cacheKey))
            {
                var cached = await _cacheProvider.GetAsync<string>(cacheKey);
                return !string.IsNullOrEmpty(cached);
            }

            var platformId = await _coinGeckoService.GetPlatformIdForChainAsync(chainId);
            await _cacheProvider.SetAsync(cacheKey, platformId ?? "", _platformsCacheExpiry);

            return !string.IsNullOrEmpty(platformId);
        }

        public async Task PreloadCacheAsync(IEnumerable<long> chainIds)
        {
            foreach (var chainId in chainIds)
            {
                await GetTokensAsync(chainId);
            }
        }
    }
}
