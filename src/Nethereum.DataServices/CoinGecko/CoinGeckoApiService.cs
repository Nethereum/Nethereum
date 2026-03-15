using Nethereum.Util.Rest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko.Responses;

namespace Nethereum.DataServices.CoinGecko
{
    public class PriceBatchResult
    {
        public Dictionary<string, Dictionary<string, decimal>> Prices { get; set; } = new Dictionary<string, Dictionary<string, decimal>>();
        public List<PriceBatchError> Errors { get; set; } = new List<PriceBatchError>();
        public bool HasErrors => Errors.Count > 0;
        public bool HasRateLimitError => Errors.Any(e => e.IsRateLimited);
    }

    public class PriceBatchError
    {
        public List<string> FailedIds { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        public bool IsRateLimited { get; set; }
    }

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

    public class CoinGeckoApiConfiguration
    {
        public string ApiKey { get; set; }
        public bool IsProApi { get; set; }

        public string BaseApiUrl => IsProApi
            ? "https://pro-api.coingecko.com/api/v3"
            : "https://api.coingecko.com/api/v3";

        public string ApiKeyHeaderName => IsProApi
            ? "x-cg-pro-api-key"
            : "x-cg-demo-api-key";

        public static CoinGeckoApiConfiguration Demo(string apiKey = null) =>
            new CoinGeckoApiConfiguration { ApiKey = apiKey, IsProApi = false };

        public static CoinGeckoApiConfiguration Pro(string apiKey) =>
            new CoinGeckoApiConfiguration { ApiKey = apiKey, IsProApi = true };
    }

    internal class TokenListCacheEntry
    {
        public CoinGeckoTokenList List { get; set; }
        public DateTime Expiry { get; set; }
    }

    public class CoinGeckoApiService
    {
        public const string DefaultBaseApiUrl = "https://api.coingecko.com/api/v3";
        public const string TokenListUrlTemplate = "https://tokens.coingecko.com/{0}/all.json";

        private readonly string _baseApiUrl;
        private readonly IRestHttpHelper _restHttpHelper;
        private readonly CoinGeckoCacheConfiguration _cacheConfig;
        private readonly Dictionary<string, string> _defaultHeaders;

        private ConcurrentDictionary<long, string> _chainToPlatformCache;
        private List<CoinGeckoAssetPlatform> _platformsCache;
        private DateTime _platformsCacheExpiry = DateTime.MinValue;

        private List<CoinGeckoCoin> _coinsListCache;
        private DateTime _coinsListCacheExpiry = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, TokenListCacheEntry> _tokenListCache = new ConcurrentDictionary<string, TokenListCacheEntry>();

        public CoinGeckoApiService(HttpClient httpClient) : this(httpClient, CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(HttpClient httpClient, CoinGeckoCacheConfiguration cacheConfiguration, CoinGeckoApiConfiguration apiConfiguration = null)
        {
            var config = apiConfiguration ?? CoinGeckoApiConfiguration.Demo();
            _baseApiUrl = config.BaseApiUrl;
            _restHttpHelper = new RestHttpHelper(httpClient);
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
            _defaultHeaders = BuildDefaultHeaders(config);
        }

        public CoinGeckoApiService() : this(CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(CoinGeckoCacheConfiguration cacheConfiguration, CoinGeckoApiConfiguration apiConfiguration = null)
        {
            var config = apiConfiguration ?? CoinGeckoApiConfiguration.Demo();
            _baseApiUrl = config.BaseApiUrl;
            _restHttpHelper = new RestHttpHelper(new HttpClient());
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
            _defaultHeaders = BuildDefaultHeaders(config);
        }

        public CoinGeckoApiService(IRestHttpHelper restHttpHelper) : this(restHttpHelper, CoinGeckoCacheConfiguration.Default)
        {
        }

        public CoinGeckoApiService(IRestHttpHelper restHttpHelper, CoinGeckoCacheConfiguration cacheConfiguration, CoinGeckoApiConfiguration apiConfiguration = null)
        {
            var config = apiConfiguration ?? CoinGeckoApiConfiguration.Demo();
            _baseApiUrl = config.BaseApiUrl;
            _restHttpHelper = restHttpHelper;
            _cacheConfig = cacheConfiguration ?? CoinGeckoCacheConfiguration.Default;
            _defaultHeaders = BuildDefaultHeaders(config);
        }

        private static Dictionary<string, string> BuildDefaultHeaders(CoinGeckoApiConfiguration apiConfiguration)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" },
                { "User-Agent", "Nethereum" }
            };

            if (!string.IsNullOrEmpty(apiConfiguration?.ApiKey))
            {
                headers[apiConfiguration.ApiKeyHeaderName] = apiConfiguration.ApiKey;
            }

            return headers;
        }

        private string AssetPlatformsUrl => _baseApiUrl + "/asset_platforms";
        private string CoinsListUrl => _baseApiUrl + "/coins/list?include_platform=true";
        private string SimplePriceUrl => _baseApiUrl + "/simple/price";
        private string SimpleTokenPriceUrlTemplate => _baseApiUrl + "/simple/token_price/{0}";

        public async Task<List<CoinGeckoAssetPlatform>> GetAssetPlatformsAsync(bool forceRefresh = false)
        {
            if (_cacheConfig.Enabled && !forceRefresh && _platformsCache != null && DateTime.UtcNow < _platformsCacheExpiry)
            {
                return _platformsCache;
            }

            var platforms = await _restHttpHelper.GetAsync<List<CoinGeckoAssetPlatform>>(AssetPlatformsUrl, _defaultHeaders);

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

            var coinsList = await _restHttpHelper.GetAsync<List<CoinGeckoCoin>>(CoinsListUrl, _defaultHeaders);

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

            try
            {
                var tokenList = await _restHttpHelper.GetAsync<CoinGeckoTokenList>(url, _defaultHeaders);
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
            var batchResult = await GetPricesWithErrorsAsync(geckoIds, vsCurrency);
            return batchResult.Prices;
        }

        public async Task<PriceBatchResult> GetPricesWithErrorsAsync(
            IEnumerable<string> geckoIds,
            string vsCurrency = "usd")
        {
            var result = new PriceBatchResult();

            if (geckoIds == null || !geckoIds.Any())
            {
                return result;
            }

            var idsList = geckoIds.Distinct().ToList();

            var batchSize = 250;
            var isFirstBatch = true;
            for (int i = 0; i < idsList.Count; i += batchSize)
            {
                if (!isFirstBatch && _cacheConfig.RateLimitDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_cacheConfig.RateLimitDelay).ConfigureAwait(false);
                }
                isFirstBatch = false;

                var batch = idsList.Skip(i).Take(batchSize).ToList();
                var idsParam = string.Join(",", batch);
                var url = $"{SimplePriceUrl}?ids={Uri.EscapeDataString(idsParam)}&vs_currencies={vsCurrency}";

                try
                {
                    var batchResult = await _restHttpHelper.GetAsync<Dictionary<string, Dictionary<string, JsonElement>>>(url, _defaultHeaders);
                    if (batchResult != null)
                    {
                        foreach (var kvp in batchResult)
                        {
                            var parsed = SafeParseJsonPrices(kvp.Value);
                            if (parsed.Count > 0)
                            {
                                result.Prices[kvp.Key] = parsed;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var isRateLimited = IsRateLimitError(ex);
                    result.Errors.Add(new PriceBatchError
                    {
                        FailedIds = batch,
                        ErrorMessage = ex.Message,
                        IsRateLimited = isRateLimited
                    });
                    if (isRateLimited) break;
                }
            }

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, decimal>>> GetTokenPricesByContractAsync(
            string platformId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd")
        {
            var batchResult = await GetTokenPricesByContractWithErrorsAsync(platformId, contractAddresses, vsCurrency);
            return batchResult.Prices;
        }

        public async Task<PriceBatchResult> GetTokenPricesByContractWithErrorsAsync(
            string platformId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd")
        {
            var result = new PriceBatchResult();

            if (string.IsNullOrEmpty(platformId) || contractAddresses == null || !contractAddresses.Any())
            {
                return result;
            }

            var addressList = contractAddresses.Distinct().ToList();

            var batchSize = 100;
            var isFirstBatch = true;
            for (int i = 0; i < addressList.Count; i += batchSize)
            {
                if (!isFirstBatch && _cacheConfig.RateLimitDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_cacheConfig.RateLimitDelay).ConfigureAwait(false);
                }
                isFirstBatch = false;

                var batch = addressList.Skip(i).Take(batchSize).ToList();
                var addressesParam = string.Join(",", batch);
                var url = string.Format(SimpleTokenPriceUrlTemplate, platformId) +
                          $"?contract_addresses={Uri.EscapeDataString(addressesParam)}&vs_currencies={vsCurrency}";

                try
                {
                    var batchResult = await _restHttpHelper.GetAsync<Dictionary<string, Dictionary<string, JsonElement>>>(url, _defaultHeaders);
                    if (batchResult != null)
                    {
                        foreach (var kvp in batchResult)
                        {
                            var parsed = SafeParseJsonPrices(kvp.Value);
                            if (parsed.Count > 0)
                            {
                                result.Prices[kvp.Key] = parsed;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var isRateLimited = IsRateLimitError(ex);
                    result.Errors.Add(new PriceBatchError
                    {
                        FailedIds = batch,
                        ErrorMessage = ex.Message,
                        IsRateLimited = isRateLimited
                    });
                    if (isRateLimited) break;
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

            var url = string.Format(SimpleTokenPriceUrlTemplate, platformId) +
                      $"?contract_addresses={contractAddress}&vs_currencies={vsCurrency}";

            try
            {
                var rawResult = await _restHttpHelper.GetAsync<Dictionary<string, Dictionary<string, JsonElement>>>(url, _defaultHeaders);
                if (rawResult != null &&
                    rawResult.TryGetValue(contractAddress.ToLowerInvariant(), out var rawPriceData))
                {
                    var priceData = SafeParseJsonPrices(rawPriceData);
                    if (priceData.TryGetValue(vsCurrency, out var price))
                    {
                        return price;
                    }
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

        private static Dictionary<string, decimal> SafeParseJsonPrices(Dictionary<string, JsonElement> raw)
        {
            var result = new Dictionary<string, decimal>();
            foreach (var kvp in raw)
            {
                try
                {
                    if (kvp.Value.ValueKind == JsonValueKind.Number)
                    {
                        if (kvp.Value.TryGetDecimal(out var decimalValue))
                        {
                            result[kvp.Key] = decimalValue;
                        }
                        else if (kvp.Value.TryGetDouble(out var doubleValue) &&
                                 doubleValue >= (double)decimal.MinValue &&
                                 doubleValue <= (double)decimal.MaxValue)
                        {
                            result[kvp.Key] = (decimal)doubleValue;
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        private static bool IsRateLimitError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("429") || message.Contains("rate limit") || message.Contains("too many requests");
        }
    }
}
