using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko;
using Nethereum.DataServices.CoinGecko.Responses;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.TokenServices.ERC20.Pricing.Resilience;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public class CoinGeckoPriceProvider : ITokenPriceProvider
    {
        private readonly CoinGeckoApiService _coinGeckoService;
        private readonly ICacheProvider _cacheProvider;
        private readonly ICacheProvider _priceMemoryCache;
        private readonly EmbeddedCoinMappingProvider _embeddedProvider;
        private readonly ICoinMappingDiffStorage _mappingDiffStorage;
        private readonly TimeSpan _priceCacheExpiry;
        private readonly TimeSpan _platformsCacheExpiry;
        private readonly RetryPolicy _retryPolicy;
        private readonly CircuitBreaker _circuitBreaker;

        private Dictionary<long, CoinGeckoAssetPlatform> _platformsCache;

        public List<string> LastUnmappedAddresses { get; private set; } = new List<string>();

        public CoinGeckoPriceProvider(
            CoinGeckoApiService coinGeckoService = null,
            ICacheProvider cacheProvider = null,
            TimeSpan? priceCacheExpiry = null,
            TimeSpan? platformsCacheExpiry = null,
            EmbeddedCoinMappingProvider embeddedProvider = null,
            ICoinMappingDiffStorage mappingDiffStorage = null,
            RetryPolicy retryPolicy = null,
            CircuitBreaker circuitBreaker = null)
        {
            _coinGeckoService = coinGeckoService ?? new CoinGeckoApiService();
            _cacheProvider = cacheProvider ?? new MemoryCacheProvider();
            _priceMemoryCache = new MemoryCacheProvider();
            _embeddedProvider = embeddedProvider ?? new EmbeddedCoinMappingProvider();
            _mappingDiffStorage = mappingDiffStorage;
            _priceCacheExpiry = priceCacheExpiry ?? TimeSpan.FromMinutes(5);
            _platformsCacheExpiry = platformsCacheExpiry ?? TimeSpan.FromDays(7);
            _retryPolicy = retryPolicy ?? RetryPolicy.Default;
            _circuitBreaker = circuitBreaker ?? CircuitBreaker.Default;
        }

        public CircuitState CircuitState => _circuitBreaker.State;

        private async Task<T> ExecuteWithResilienceAsync<T>(
            Func<Task<T>> action,
            Func<T> fallback = null)
        {
            if (!_circuitBreaker.AllowRequest())
            {
                return fallback != null ? fallback() : default;
            }

            for (int attempt = 0; attempt <= _retryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    var result = await action().ConfigureAwait(false);
                    _circuitBreaker.RecordSuccess();
                    return result;
                }
                catch (Exception ex) when (IsTransientError(ex) && attempt < _retryPolicy.MaxRetries)
                {
                    await Task.Delay(_retryPolicy.GetDelay(attempt)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (attempt >= _retryPolicy.MaxRetries)
                    {
                        _circuitBreaker.RecordFailure();
                        return fallback != null ? fallback() : default;
                    }
                }
            }

            _circuitBreaker.RecordFailure();
            return fallback != null ? fallback() : default;
        }

        private static bool IsTransientError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("timeout") ||
                   message.Contains("429") ||
                   message.Contains("500") ||
                   message.Contains("502") ||
                   message.Contains("503") ||
                   message.Contains("504") ||
                   message.Contains("connection") ||
                   message.Contains("temporarily");
        }

        private async Task<Dictionary<long, CoinGeckoAssetPlatform>> GetPlatformsAsync()
        {
            if (_platformsCache != null)
                return _platformsCache;

            var cacheKey = "coingecko:platforms";
            var cached = await _cacheProvider.GetAsync<Dictionary<long, CoinGeckoAssetPlatform>>(cacheKey);
            if (cached != null && cached.Count > 0)
            {
                _platformsCache = cached;
                return _platformsCache;
            }

            var platforms = await ExecuteWithResilienceAsync(
                () => _coinGeckoService.GetAssetPlatformsAsync(),
                () => new List<CoinGeckoAssetPlatform>());

            if (platforms != null && platforms.Count > 0)
            {
                _platformsCache = platforms
                    .Where(p => p.ChainIdentifier.HasValue)
                    .ToDictionary(p => p.ChainIdentifier.Value, p => p);

                await _cacheProvider.SetAsync(cacheKey, _platformsCache, _platformsCacheExpiry);
            }

            return _platformsCache ?? new Dictionary<long, CoinGeckoAssetPlatform>();
        }

        public Task<string> GetNativeCoinIdAsync(long chainId)
        {
            return Task.FromResult(_embeddedProvider.GetNativeCoinId(chainId));
        }

        public async Task<Dictionary<string, TokenPrice>> GetPricesAsync(
            IEnumerable<string> tokenIds,
            string vsCurrency = "usd")
        {
            var idsList = tokenIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (idsList == null || !idsList.Any())
            {
                return new Dictionary<string, TokenPrice>();
            }

            var result = new Dictionary<string, TokenPrice>(StringComparer.OrdinalIgnoreCase);
            var uncachedIds = new List<string>();

            foreach (var id in idsList)
            {
                var cacheKey = $"price:{id}:{vsCurrency}";
                var cached = await _priceMemoryCache.GetAsync<TokenPrice>(cacheKey);
                if (cached != null)
                {
                    result[id] = cached;
                }
                else
                {
                    uncachedIds.Add(id);
                }
            }

            if (uncachedIds.Any())
            {
                var prices = await ExecuteWithResilienceAsync(
                    () => _coinGeckoService.GetPricesAsync(uncachedIds, vsCurrency),
                    () => new Dictionary<string, Dictionary<string, decimal>>());

                foreach (var kvp in prices)
                {
                    if (kvp.Value.TryGetValue(vsCurrency, out var price))
                    {
                        var tokenPrice = new TokenPrice
                        {
                            TokenId = kvp.Key,
                            Price = price,
                            Currency = vsCurrency,
                            UpdatedAt = DateTime.UtcNow
                        };

                        result[kvp.Key] = tokenPrice;

                        var cacheKey = $"price:{kvp.Key}:{vsCurrency}";
                        await _priceMemoryCache.SetAsync(cacheKey, tokenPrice, _priceCacheExpiry);
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<string, TokenPrice>> GetPricesByContractAsync(
            long chainId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd")
        {
            var addresses = contractAddresses?.Select(a => a.ToLowerInvariant()).ToList();
            if (addresses == null || !addresses.Any())
            {
                LastUnmappedAddresses = new List<string>();
                return new Dictionary<string, TokenPrice>();
            }

            var tokenIds = await GetTokenIdsAsync(chainId, addresses);

            LastUnmappedAddresses = addresses
                .Where(a => !tokenIds.ContainsKey(a))
                .ToList();

            if (!tokenIds.Any())
            {
                return new Dictionary<string, TokenPrice>();
            }

            var prices = await GetPricesAsync(tokenIds.Values, vsCurrency);

            var result = new Dictionary<string, TokenPrice>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in tokenIds)
            {
                if (prices.TryGetValue(kvp.Value, out var price))
                {
                    price.ContractAddress = kvp.Key;
                    result[kvp.Key] = price;
                }
            }

            return result;
        }

        public async Task<string> GetTokenIdAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            var embeddedId = _embeddedProvider.GetCoinId(chainId, contractAddress);
            if (!string.IsNullOrEmpty(embeddedId))
                return embeddedId;

            if (_mappingDiffStorage != null)
            {
                var additionalMappings = await _mappingDiffStorage.GetAdditionalMappingsAsync(chainId);
                if (additionalMappings.TryGetValue(contractAddress.ToLowerInvariant(), out var id))
                    return id;
            }

            return null;
        }

        public async Task<Dictionary<string, string>> GetTokenIdsAsync(
            long chainId,
            IEnumerable<string> contractAddresses)
        {
            var addresses = contractAddresses?.Select(a => a.ToLowerInvariant()).Distinct().ToList();
            if (addresses == null || !addresses.Any())
            {
                return new Dictionary<string, string>();
            }

            var embeddedMappings = _embeddedProvider.GetCoinMappings(chainId);

            Dictionary<string, string> additionalMappings = null;
            if (_mappingDiffStorage != null)
            {
                additionalMappings = await _mappingDiffStorage.GetAdditionalMappingsAsync(chainId);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var address in addresses)
            {
                if (embeddedMappings.TryGetValue(address, out var id))
                {
                    result[address] = id;
                }
                else if (additionalMappings != null && additionalMappings.TryGetValue(address, out id))
                {
                    result[address] = id;
                }
            }

            return result;
        }

        public async Task<TokenPrice> GetNativeTokenPriceAsync(long chainId, string vsCurrency = "usd")
        {
            var nativeCoinId = await GetNativeCoinIdAsync(chainId);
            if (string.IsNullOrEmpty(nativeCoinId))
                return null;

            var prices = await GetPricesAsync(new[] { nativeCoinId }, vsCurrency);
            prices.TryGetValue(nativeCoinId, out var price);
            return price;
        }

        public async Task InvalidatePriceAsync(string tokenId, string vsCurrency = "usd")
        {
            if (string.IsNullOrEmpty(tokenId))
                return;

            var cacheKey = $"price:{tokenId.ToLowerInvariant()}:{vsCurrency}";
            await _priceMemoryCache.RemoveAsync(cacheKey);
        }

        public async Task InvalidateAllPricesAsync()
        {
            if (_priceMemoryCache is MemoryCacheProvider memoryCache)
            {
                memoryCache.Clear();
            }
        }

        public void ClearPlatformsCache()
        {
            _platformsCache = null;
        }

        public bool SupportsChain(long chainId)
        {
            return _embeddedProvider.SupportsChain(chainId);
        }
    }
}
