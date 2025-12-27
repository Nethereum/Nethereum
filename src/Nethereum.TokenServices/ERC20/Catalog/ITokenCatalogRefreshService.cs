using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.ERC20.Catalog
{
    public interface ITokenCatalogRefreshService
    {
        Task<CatalogRefreshResult> RefreshAsync(long chainId, CatalogRefreshOptions options = null, CancellationToken ct = default);
        Task<bool> ShouldRefreshAsync(long chainId, CancellationToken ct = default);
        void RegisterSource(ITokenCatalogRefreshSource source);
        IReadOnlyList<ITokenCatalogRefreshSource> GetRegisteredSources();
    }

    public class CatalogRefreshOptions
    {
        public bool ForceRefresh { get; set; }
        public bool IncrementalOnly { get; set; } = true;
        public string PreferredSource { get; set; }
        public TimeSpan? MinRefreshInterval { get; set; }
        public bool UpdateExistingTokens { get; set; }
    }

    public class CatalogRefreshResult
    {
        public bool Success { get; set; }
        public int TotalTokensAdded { get; set; }
        public int TotalTokensUpdated { get; set; }
        public int TotalTokensInCatalog { get; set; }
        public DateTime? RefreshCompletedUtc { get; set; }
        public string SourceUsed { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
        public bool WasSkipped { get; set; }
        public string SkipReason { get; set; }
    }
}
