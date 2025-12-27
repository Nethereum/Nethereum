using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog.Migration
{
    public class TokenCatalogMigrationService
    {
        private readonly ITokenCatalogRepository _catalogRepository;
        private readonly ITokenListDiffStorage _legacyDiffStorage;
        private readonly EmbeddedTokenListProvider _embeddedProvider;

        public TokenCatalogMigrationService(
            ITokenCatalogRepository catalogRepository,
            ITokenListDiffStorage legacyDiffStorage = null,
            EmbeddedTokenListProvider embeddedProvider = null)
        {
            _catalogRepository = catalogRepository ?? throw new ArgumentNullException(nameof(catalogRepository));
            _legacyDiffStorage = legacyDiffStorage;
            _embeddedProvider = embeddedProvider ?? new EmbeddedTokenListProvider();
        }

        public async Task<MigrationResult> MigrateAsync(
            long chainId,
            MigrationOptions options = null,
            CancellationToken ct = default)
        {
            options ??= new MigrationOptions();
            var result = new MigrationResult { ChainId = chainId };

            try
            {
                var isInitialized = await _catalogRepository.IsInitializedAsync(chainId, ct).ConfigureAwait(false);

                if (isInitialized && !options.ForceMigration)
                {
                    result.WasSkipped = true;
                    result.SkipReason = "Catalog already initialized";
                    result.Success = true;
                    return result;
                }

                var embeddedTokens = await _embeddedProvider.GetTokensAsync(chainId).ConfigureAwait(false);
                result.EmbeddedTokenCount = embeddedTokens?.Count ?? 0;

                List<TokenInfo> diffTokens = null;
                if (_legacyDiffStorage != null)
                {
                    diffTokens = await _legacyDiffStorage.GetAdditionalTokensAsync(chainId).ConfigureAwait(false);
                    result.DiffTokenCount = diffTokens?.Count ?? 0;
                }

                var allTokens = new Dictionary<string, CatalogTokenInfo>(StringComparer.OrdinalIgnoreCase);
                var now = DateTime.UtcNow;

                if (embeddedTokens != null)
                {
                    foreach (var token in embeddedTokens)
                    {
                        if (!string.IsNullOrEmpty(token.Address))
                        {
                            var normalizedAddress = token.Address.ToLowerInvariant();
                            allTokens[normalizedAddress] = CatalogTokenInfo.FromTokenInfo(token, "embedded");
                            allTokens[normalizedAddress].AddedAtUtc = now;
                        }
                    }
                }

                if (diffTokens != null)
                {
                    DateTime? legacyLastUpdate = null;
                    if (_legacyDiffStorage != null)
                    {
                        legacyLastUpdate = await _legacyDiffStorage.GetLastUpdateAsync(chainId).ConfigureAwait(false);
                    }

                    foreach (var token in diffTokens)
                    {
                        if (!string.IsNullOrEmpty(token.Address))
                        {
                            var normalizedAddress = token.Address.ToLowerInvariant();

                            if (allTokens.TryGetValue(normalizedAddress, out var existing))
                            {
                                existing.UpdateFrom(CatalogTokenInfo.FromTokenInfo(token, "migrated"));
                            }
                            else
                            {
                                var catalogToken = CatalogTokenInfo.FromTokenInfo(token, "migrated");
                                catalogToken.AddedAtUtc = legacyLastUpdate ?? now;
                                allTokens[normalizedAddress] = catalogToken;
                            }
                        }
                    }
                }

                if (options.ForceMigration)
                {
                    await _catalogRepository.ClearAsync(chainId, ct).ConfigureAwait(false);
                }

                if (allTokens.Count > 0)
                {
                    var addedCount = await _catalogRepository.AddOrUpdateTokensAsync(
                        chainId,
                        allTokens.Values,
                        updateExisting: true,
                        ct).ConfigureAwait(false);

                    result.MigratedTokenCount = addedCount;
                }

                var metadata = new CatalogMetadata
                {
                    IsSeeded = true,
                    SeededAtUtc = now,
                    TokenCount = allTokens.Count,
                    LastRefreshSource = "migration"
                };
                await _catalogRepository.SetMetadataAsync(chainId, metadata, ct).ConfigureAwait(false);

                result.Success = true;
                result.TotalTokenCount = allTokens.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<List<MigrationResult>> MigrateAllChainsAsync(
            MigrationOptions options = null,
            CancellationToken ct = default)
        {
            var results = new List<MigrationResult>();
            var supportedChains = EmbeddedTokenListProvider.GetSupportedChainIds();

            foreach (var chainId in supportedChains)
            {
                ct.ThrowIfCancellationRequested();
                var result = await MigrateAsync(chainId, options, ct).ConfigureAwait(false);
                results.Add(result);
            }

            return results;
        }
    }

    public class MigrationOptions
    {
        public bool ForceMigration { get; set; }
        public bool ClearLegacyAfterMigration { get; set; }
    }

    public class MigrationResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public bool WasSkipped { get; set; }
        public string SkipReason { get; set; }
        public string ErrorMessage { get; set; }
        public int EmbeddedTokenCount { get; set; }
        public int DiffTokenCount { get; set; }
        public int MigratedTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }
}
