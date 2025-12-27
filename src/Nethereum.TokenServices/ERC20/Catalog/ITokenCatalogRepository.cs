using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog
{
    public interface ITokenCatalogRepository
    {
        Task<List<CatalogTokenInfo>> GetAllTokensAsync(long chainId, CancellationToken ct = default);
        Task<List<CatalogTokenInfo>> GetTokensAddedSinceAsync(long chainId, DateTime sinceUtc, CancellationToken ct = default);
        Task<CatalogTokenInfo> GetTokenByAddressAsync(long chainId, string contractAddress, CancellationToken ct = default);
        Task<int> GetTokenCountAsync(long chainId, CancellationToken ct = default);
        Task<int> AddOrUpdateTokensAsync(long chainId, IEnumerable<CatalogTokenInfo> tokens, bool updateExisting = false, CancellationToken ct = default);
        Task<bool> RemoveTokenAsync(long chainId, string contractAddress, CancellationToken ct = default);
        Task<bool> IsInitializedAsync(long chainId, CancellationToken ct = default);
        Task SeedFromEmbeddedAsync(long chainId, bool forceReseed = false, CancellationToken ct = default);
        Task<CatalogMetadata> GetMetadataAsync(long chainId, CancellationToken ct = default);
        Task SetMetadataAsync(long chainId, CatalogMetadata metadata, CancellationToken ct = default);
        Task ClearAsync(long chainId, CancellationToken ct = default);
    }

    public class CatalogMetadata
    {
        public DateTime? LastRefreshUtc { get; set; }
        public DateTime? LastFullRefreshUtc { get; set; }
        public string LastRefreshSource { get; set; }
        public int TokenCount { get; set; }
        public bool IsSeeded { get; set; }
        public DateTime? SeededAtUtc { get; set; }
    }
}
