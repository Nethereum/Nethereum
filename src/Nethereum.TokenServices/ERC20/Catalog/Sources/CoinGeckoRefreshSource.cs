using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog.Sources
{
    public class CoinGeckoRefreshSource : ITokenCatalogRefreshSource
    {
        private readonly CoinGeckoApiService _coinGeckoService;
        private readonly TimeSpan _rateLimitDelay;
        private DateTime? _lastRequestTime;
        private bool _isRateLimited;
        private DateTime? _rateLimitResetTime;

        public string SourceName => "coingecko";
        public int Priority => 10;

        public CoinGeckoRefreshSource(
            CoinGeckoApiService coinGeckoService = null,
            TimeSpan? rateLimitDelay = null)
        {
            _coinGeckoService = coinGeckoService ?? new CoinGeckoApiService();
            _rateLimitDelay = rateLimitDelay ?? TimeSpan.FromSeconds(1.5);
        }

        public async Task<bool> SupportsChainAsync(long chainId, CancellationToken ct = default)
        {
            try
            {
                var platformId = await _coinGeckoService.GetPlatformIdForChainAsync(chainId).ConfigureAwait(false);
                return !string.IsNullOrEmpty(platformId);
            }
            catch
            {
                return false;
            }
        }

        public async Task<TokenCatalogRefreshResult> FetchTokensAsync(
            long chainId,
            DateTime? sinceUtc = null,
            CancellationToken ct = default)
        {
            var result = new TokenCatalogRefreshResult
            {
                SourceName = SourceName
            };

            try
            {
                await RespectRateLimitAsync(ct).ConfigureAwait(false);

                var geckoTokens = await _coinGeckoService.GetTokensForChainAsync(chainId, true).ConfigureAwait(false);

                _lastRequestTime = DateTime.UtcNow;
                _isRateLimited = false;

                if (geckoTokens == null || geckoTokens.Count == 0)
                {
                    result.Success = true;
                    result.Tokens = new List<CatalogTokenInfo>();
                    return result;
                }

                var catalogTokens = new List<CatalogTokenInfo>();
                var now = DateTime.UtcNow;

                foreach (var token in geckoTokens)
                {
                    if (string.IsNullOrEmpty(token.Address))
                        continue;

                    var catalogToken = new CatalogTokenInfo
                    {
                        Address = token.Address,
                        Symbol = token.Symbol,
                        Name = token.Name,
                        Decimals = token.Decimals,
                        LogoUri = token.LogoURI,
                        ChainId = chainId,
                        AddedAtUtc = now,
                        Source = SourceName
                    };

                    catalogTokens.Add(catalogToken);
                }

                result.Success = true;
                result.Tokens = catalogTokens;
                result.NewTokenCount = catalogTokens.Count;
            }
            catch (Exception ex)
            {
                if (IsRateLimitException(ex))
                {
                    _isRateLimited = true;
                    _rateLimitResetTime = DateTime.UtcNow.AddMinutes(1);
                    result.Success = false;
                    result.ErrorMessage = "Rate limited by CoinGecko API";
                    result.NextAllowedRefreshUtc = _rateLimitResetTime;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }
            }

            return result;
        }

        public Task<RateLimitInfo> GetRateLimitInfoAsync(CancellationToken ct = default)
        {
            var info = new RateLimitInfo
            {
                IsRateLimited = _isRateLimited,
                ResetAtUtc = _rateLimitResetTime,
                RecommendedDelay = _rateLimitDelay
            };

            return Task.FromResult(info);
        }

        private async Task RespectRateLimitAsync(CancellationToken ct)
        {
            if (_lastRequestTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - _lastRequestTime.Value;
                if (elapsed < _rateLimitDelay)
                {
                    var delay = _rateLimitDelay - elapsed;
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        private static bool IsRateLimitException(Exception ex)
        {
            var message = ex.Message.ToLowerInvariant();
            return message.Contains("429") ||
                   message.Contains("rate limit") ||
                   message.Contains("too many requests");
        }
    }
}
