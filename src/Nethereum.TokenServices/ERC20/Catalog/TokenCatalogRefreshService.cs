using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog
{
    public class TokenCatalogRefreshService : ITokenCatalogRefreshService
    {
        private readonly ITokenCatalogRepository _repository;
        private readonly List<ITokenCatalogRefreshSource> _sources = new();
        private readonly object _sourceLock = new();
        private readonly TimeSpan _defaultMinInterval = TimeSpan.FromHours(6);

        public TokenCatalogRefreshService(ITokenCatalogRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public TokenCatalogRefreshService(
            ITokenCatalogRepository repository,
            IEnumerable<ITokenCatalogRefreshSource> sources)
            : this(repository)
        {
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    RegisterSource(source);
                }
            }
        }

        public void RegisterSource(ITokenCatalogRefreshSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            lock (_sourceLock)
            {
                if (!_sources.Any(s => s.SourceName == source.SourceName))
                {
                    _sources.Add(source);
                    _sources.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                }
            }
        }

        public IReadOnlyList<ITokenCatalogRefreshSource> GetRegisteredSources()
        {
            lock (_sourceLock)
            {
                return _sources.ToList().AsReadOnly();
            }
        }

        public async Task<bool> ShouldRefreshAsync(long chainId, CancellationToken ct = default)
        {
            var metadata = await _repository.GetMetadataAsync(chainId, ct).ConfigureAwait(false);

            if (!metadata.IsSeeded)
                return true;

            if (!metadata.LastRefreshUtc.HasValue)
                return true;

            var timeSinceLastRefresh = DateTime.UtcNow - metadata.LastRefreshUtc.Value;
            return timeSinceLastRefresh >= _defaultMinInterval;
        }

        public async Task<CatalogRefreshResult> RefreshAsync(
            long chainId,
            CatalogRefreshOptions options = null,
            CancellationToken ct = default)
        {
            options ??= new CatalogRefreshOptions();
            var sw = Stopwatch.StartNew();
            var result = new CatalogRefreshResult();

            try
            {
                var isInitialized = await _repository.IsInitializedAsync(chainId, ct).ConfigureAwait(false);
                if (!isInitialized)
                {
                    await _repository.SeedFromEmbeddedAsync(chainId, false, ct).ConfigureAwait(false);
                }

                if (!options.ForceRefresh)
                {
                    var shouldRefresh = await ShouldRefreshAsync(chainId, ct).ConfigureAwait(false);
                    if (!shouldRefresh)
                    {
                        result.WasSkipped = true;
                        result.SkipReason = "Refresh interval not elapsed";
                        result.Success = true;
                        return result;
                    }
                }

                var sources = GetSourcesForRefresh(options.PreferredSource);
                if (!sources.Any())
                {
                    result.WasSkipped = true;
                    result.SkipReason = "No refresh sources registered";
                    result.Success = true;
                    return result;
                }

                DateTime? sinceUtc = null;
                if (options.IncrementalOnly)
                {
                    var metadata = await _repository.GetMetadataAsync(chainId, ct).ConfigureAwait(false);
                    sinceUtc = metadata.LastRefreshUtc;
                }

                foreach (var source in sources)
                {
                    ct.ThrowIfCancellationRequested();

                    var supportsChain = await source.SupportsChainAsync(chainId, ct).ConfigureAwait(false);
                    if (!supportsChain)
                        continue;

                    var rateLimitInfo = await source.GetRateLimitInfoAsync(ct).ConfigureAwait(false);
                    if (rateLimitInfo.IsRateLimited && rateLimitInfo.ResetAtUtc.HasValue)
                    {
                        if (DateTime.UtcNow < rateLimitInfo.ResetAtUtc.Value)
                        {
                            result.Warnings.Add($"Source {source.SourceName} is rate limited until {rateLimitInfo.ResetAtUtc}");
                            continue;
                        }
                    }

                    var sourceResult = await source.FetchTokensAsync(chainId, sinceUtc, ct).ConfigureAwait(false);

                    if (!sourceResult.Success)
                    {
                        result.Warnings.Add($"Source {source.SourceName} failed: {sourceResult.ErrorMessage}");
                        continue;
                    }

                    if (sourceResult.Tokens != null && sourceResult.Tokens.Count > 0)
                    {
                        var addedCount = await _repository.AddOrUpdateTokensAsync(
                            chainId,
                            sourceResult.Tokens,
                            options.UpdateExistingTokens,
                            ct).ConfigureAwait(false);

                        result.TotalTokensAdded += addedCount;
                        result.TotalTokensUpdated += sourceResult.UpdatedTokenCount;
                    }

                    result.SourceUsed = source.SourceName;
                    result.Success = true;
                    break;
                }

                if (result.Success)
                {
                    var tokenCount = await _repository.GetTokenCountAsync(chainId, ct).ConfigureAwait(false);
                    result.TotalTokensInCatalog = tokenCount;

                    var updatedMetadata = await _repository.GetMetadataAsync(chainId, ct).ConfigureAwait(false);
                    updatedMetadata.LastRefreshUtc = DateTime.UtcNow;
                    updatedMetadata.LastRefreshSource = result.SourceUsed;
                    updatedMetadata.TokenCount = tokenCount;

                    if (!options.IncrementalOnly)
                    {
                        updatedMetadata.LastFullRefreshUtc = DateTime.UtcNow;
                    }

                    await _repository.SetMetadataAsync(chainId, updatedMetadata, ct).ConfigureAwait(false);
                }
                else if (!result.WasSkipped)
                {
                    result.ErrorMessage = "All refresh sources failed";
                }

                result.RefreshCompletedUtc = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Refresh was cancelled";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }

        private IEnumerable<ITokenCatalogRefreshSource> GetSourcesForRefresh(string preferredSource)
        {
            lock (_sourceLock)
            {
                if (!string.IsNullOrEmpty(preferredSource))
                {
                    var preferred = _sources.FirstOrDefault(s =>
                        s.SourceName.Equals(preferredSource, StringComparison.OrdinalIgnoreCase));

                    if (preferred != null)
                    {
                        return new[] { preferred };
                    }
                }

                return _sources.ToList();
            }
        }
    }
}
