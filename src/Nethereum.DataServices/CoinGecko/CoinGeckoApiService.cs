using Nethereum.Util.Rest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko.Responses;

namespace Nethereum.DataServices.CoinGecko
{
    public class CoinGeckoCacheConfiguration
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan PlatformsCacheDuration { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan CoinsListCacheDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan TokenListCacheDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan RateLimitDelay { get; set; } = TimeSpan.FromMilliseconds(1500);

        public static CoinGeckoCacheConfiguration Default => new CoinGeckoCacheConfiguration();
        public static CoinGeckoCacheConfiguration Disabled => new CoinGeckoCacheConfiguration { Enabled = false };
    }

    internal class TokenListCacheEntry
    {
        public CoinGeckoTokenList List { get; set; }
        public DateTime Expiry { get; set; }
    }

    public class CoinGeckoApiService
    {
        public const string BaseApiUrl = "https://api.coingecko.com/api/v3";
        public const string AssetPlatformsUrl = BaseApiUrl + "/asset_platforms";
        public const string CoinsListUrl = BaseApiUrl + "/coins/list?include_platform=true";
        public const string SimplePriceUrl = BaseApiUrl + "/simple/price";
        public const string TokenPriceUrlTemplate = BaseApiUrl + "/simple/token_price/{0}";
        public const string TokenListUrlTemplate = "https://tokens.coingecko.com/{0}/all.json";

        private readonly IRestHttpHelper _restHttpHelper;
        private readonly CoinGeckoCacheConfiguration _cacheConfig;

        private ConcurrentDictionary<long, string> _chainToPlatformCache;
        private List<CoinGeckoAssetPlatform> _platformsCache;
        private DateTime _platformsCacheExpiry = DateTime.MinValue;

        private List<CoinGeckoCoin> _coinsListCache;
        private DateTime _coinsListCacheExpiry = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, TokenListCacheEntry> _tokenListCache = new ConcurrentDictionary<string, TokenListCacheEntry>();

        public CoinGeckoApiService(HttpClient httpClient) : this(httpClient, CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(HttpClient httpClient, CoinGeckoCacheConfiguration cacheConfiguration)
        {
            _restHttpHelper = new RestHttpHelper(httpClient);
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
        }

        public CoinGeckoApiService() : this(CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(CoinGeckoCacheConfiguration cacheConfiguration)
        {
            _restHttpHelper = new RestHttpHelper(new HttpClient());
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
        }

        public CoinGeckoApiService(IRestHttpHelper restHttpHelper) : this(restHttpHelper, CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(IRestHttpHelper restHttpHelper, CoinGeckoCacheConfiguration cacheConfiguration)
        {
            _restHttpHelper = restHttpHelper;
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
        }

        public async Task<List<CoinGeckoAssetPlatform>> GetAssetPlatformsAsync(bool forceRefresh = false)
        {
            if (_cacheConfig.Enabled && !forceRefresh && _platformsCache != null && DateTime.UtcNow < _platformsCacheExpiry)
            {
                return _platformsCache;
            }

            var headers = new Dictionary<string, string> { { "accept", "application/json" } };
            var platforms = await _restHttpHelper.GetAsync<List<CoinGeckoAssetPlatform>>(AssetPlatformsUrl, headers);

            if (_cacheConfig.Enabled)
            {
                _platformsCache = platforms;
                _platformsCacheExpiry = DateTime.UtcNow.Add(_cacheConfig.PlatformsCacheDuration);

                _chainToPlatformCache = new ConcurrentDictionary<long, string>(
                    _platformsCache
                        .Where(p => p.ChainIdentifier.HasValue && !string.IsNullOrEmpty(p.Id))
                        .ToDictionary(p => p.ChainIdentifier.Value, p => p.Id)
                );
            }

            return platforms;
        }

        public async Task<string> GetPlatformIdForChainAsync(long chainId)
        {
            if (_chainToPlatformCache == null || !_chainToPlatformCache.Any())
            {
                await GetAssetPlatformsAsync();
            }

            return _chainToPlatformCache != null && _chainToPlatformCache.TryGetValue(chainId, out var platformId)
                ? platformId
                : null;
        }

        public async Task<List<CoinGeckoCoin>> GetCoinsListAsync(bool forceRefresh = false)
        {
            if (_cacheConfig.Enabled && !forceRefresh && _coinsListCache != null && DateTime.UtcNow < _coinsListCacheExpiry)
            {
                return _coinsListCache;
            }

            var headers = new Dictionary<string, string> { { "accept", "application/json" } };
            var coinsList = await _restHttpHelper.GetAsync<List<CoinGeckoCoin>>(CoinsListUrl, headers);

            if (_cacheConfig.Enabled)
            {
                _coinsListCache = coinsList;
                _coinsListCacheExpiry = DateTime.UtcNow.Add(_cacheConfig.CoinsListCacheDuration);
            }

            return coinsList;
        }

        public async Task<CoinGeckoTokenList> GetTokenListForPlatformAsync(string platformId, bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(platformId))
            {
                return null;
            }

            if (_cacheConfig.Enabled && !forceRefresh && _tokenListCache.TryGetValue(platformId, out var cached) && DateTime.UtcNow < cached.Expiry)
            {
                return cached.List;
            }

            var url = string.Format(TokenListUrlTemplate, platformId);
            var headers = new Dictionary<string, string> { { "accept", "application/json" } };

            try
            {
                var tokenList = await _restHttpHelper.GetAsync<CoinGeckoTokenList>(url, headers);
                if (_cacheConfig.Enabled)
                {
                    _tokenListCache[platformId] = new TokenListCacheEntry { List = tokenList, Expiry = DateTime.UtcNow.Add(_cacheConfig.TokenListCacheDuration) };
                }
                return tokenList;
            }
            catch
            {
                return null;
            }
        }

        public async Task<CoinGeckoTokenList> GetTokenListForChainAsync(long chainId, bool forceRefresh = false)
        {
            var platformId = await GetPlatformIdForChainAsync(chainId);
            if (string.IsNullOrEmpty(platformId))
            {
                return null;
            }

            return await GetTokenListForPlatformAsync(platformId, forceRefresh);
        }

        public async Task<List<CoinGeckoToken>> GetTokensForChainAsync(long chainId, bool forceRefresh = false)
        {
            var tokenList = await GetTokenListForChainAsync(chainId, forceRefresh);
            return tokenList?.Tokens ?? new List<CoinGeckoToken>();
        }

        public async Task<Dictionary<string, Dictionary<string, decimal>>> GetPricesAsync(
            IEnumerable<string> geckoIds,
            string vsCurrency = "usd")
        {
            if (geckoIds == null || !geckoIds.Any())
            {
                return new Dictionary<string, Dictionary<string, decimal>>();
            }

            var idsList = geckoIds.Distinct().ToList();
            var result = new Dictionary<string, Dictionary<string, decimal>>();

            var batchSize = 250;
            var isFirstBatch = true;
            for (int i = 0; i < idsList.Count; i += batchSize)
            {
                if (!isFirstBatch && _cacheConfig.RateLimitDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_cacheConfig.RateLimitDelay).ConfigureAwait(false);
                }
                isFirstBatch = false;

                var batch = idsList.Skip(i).Take(batchSize);
                var idsParam = string.Join(",", batch);
                var url = $"{SimplePriceUrl}?ids={Uri.EscapeDataString(idsParam)}&vs_currencies={vsCurrency}";
                var headers = new Dictionary<string, string> { { "accept", "application/json" } };

                try
                {
                    var batchResult = await _restHttpHelper.GetAsync<Dictionary<string, Dictionary<string, decimal>>>(url, headers);
                    if (batchResult != null)
                    {
                        foreach (var kvp in batchResult)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        public async Task<decimal?> GetTokenPriceByContractAsync(
            string platformId,
            string contractAddress,
            string vsCurrency = "usd")
        {
            if (string.IsNullOrEmpty(platformId) || string.IsNullOrEmpty(contractAddress))
            {
                return null;
            }

            var url = string.Format(TokenPriceUrlTemplate, platformId) +
                      $"?contract_addresses={contractAddress}&vs_currencies={vsCurrency}";
            var headers = new Dictionary<string, string> { { "accept", "application/json" } };

            try
            {
                var result = await _restHttpHelper.GetAsync<Dictionary<string, Dictionary<string, decimal>>>(url, headers);
                if (result != null &&
                    result.TryGetValue(contractAddress.ToLowerInvariant(), out var priceData) &&
                    priceData.TryGetValue(vsCurrency, out var price))
                {
                    return price;
                }
            }
            catch
            {
            }

            return null;
        }

        public async Task<string> FindCoinGeckoIdAsync(string contractAddress, long chainId)
        {
            var platformId = await GetPlatformIdForChainAsync(chainId);
            if (string.IsNullOrEmpty(platformId))
            {
                return null;
            }

            var coinsList = await GetCoinsListAsync();
            if (coinsList == null)
            {
                return null;
            }

            var normalizedAddress = contractAddress.ToLowerInvariant();

            foreach (var coin in coinsList)
            {
                if (coin.Platforms != null &&
                    coin.Platforms.TryGetValue(platformId, out var address) &&
                    !string.IsNullOrEmpty(address) &&
                    address.Equals(normalizedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return coin.Id;
                }
            }

            return null;
        }

        public async Task<Dictionary<string, string>> FindCoinGeckoIdsAsync(
            IEnumerable<string> contractAddresses,
            long chainId)
        {
            var platformId = await GetPlatformIdForChainAsync(chainId);
            if (string.IsNullOrEmpty(platformId))
            {
                return new Dictionary<string, string>();
            }

            var coinsList = await GetCoinsListAsync();
            if (coinsList == null)
            {
                return new Dictionary<string, string>();
            }

            var normalizedAddresses = new HashSet<string>(
                contractAddresses.Select(a => a.ToLowerInvariant()));

            var result = new Dictionary<string, string>();

            foreach (var coin in coinsList)
            {
                if (coin.Platforms != null &&
                    coin.Platforms.TryGetValue(platformId, out var address) &&
                    !string.IsNullOrEmpty(address))
                {
                    var normalizedCoinAddress = address.ToLowerInvariant();
                    if (normalizedAddresses.Contains(normalizedCoinAddress))
                    {
                        result[normalizedCoinAddress] = coin.Id;
                    }
                }
            }

            return result;
        }

        public void ClearCache()
        {
            _platformsCache = null;
            _platformsCacheExpiry = DateTime.MinValue;
            _chainToPlatformCache = null;
            _coinsListCache = null;
            _coinsListCacheExpiry = DateTime.MinValue;
            _tokenListCache.Clear();
        }
    }
}
