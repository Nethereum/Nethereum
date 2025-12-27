using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog
{
    public interface ITokenCatalogRefreshSource
    {
        string SourceName { get; }
        int Priority { get; }
        Task<bool> SupportsChainAsync(long chainId, CancellationToken ct = default);
        Task<TokenCatalogRefreshResult> FetchTokensAsync(long chainId, DateTime? sinceUtc = null, CancellationToken ct = default);
        Task<RateLimitInfo> GetRateLimitInfoAsync(CancellationToken ct = default);
    }

    public class TokenCatalogRefreshResult
    {
        public bool Success { get; set; }
        public List<CatalogTokenInfo> Tokens { get; set; } = new List<CatalogTokenInfo>();
        public int NewTokenCount { get; set; }
        public int UpdatedTokenCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? NextAllowedRefreshUtc { get; set; }
        public bool IsPartialResult { get; set; }
        public string SourceName { get; set; }
    }

    public class RateLimitInfo
    {
        public bool IsRateLimited { get; set; }
        public DateTime? ResetAtUtc { get; set; }
        public int? RemainingRequests { get; set; }
        public TimeSpan RecommendedDelay { get; set; } = TimeSpan.FromSeconds(1.5);
    }
}
