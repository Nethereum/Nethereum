using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Catalog;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.Caching
{
    public class FileTokenCatalogRepository : FileStorageBase, ITokenCatalogRepository
    {
        private const string CatalogFileName = "catalog.json";
        private const string MetadataFileName = "metadata.json";

        private readonly EmbeddedTokenListProvider _embeddedProvider;
        private readonly ConcurrentDictionary<long, CatalogCache> _chainCaches = new();
        private readonly SemaphoreSlim _seedLock = new(1, 1);

        public FileTokenCatalogRepository(
            string baseDirectory = null,
            EmbeddedTokenListProvider embeddedProvider = null,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
            : base(
                baseDirectory ?? GetDefaultDirectory(),
                jsonOptions,
                onError)
        {
            _embeddedProvider = embeddedProvider ?? new EmbeddedTokenListProvider();
        }

        private static string GetDefaultDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "TokenServices", "catalog");
        }

        public async Task<List<CatalogTokenInfo>> GetAllTokensAsync(long chainId, CancellationToken ct = default)
        {
            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            return cache.Tokens.Values.ToList();
        }

        public async Task<List<CatalogTokenInfo>> GetTokensAddedSinceAsync(long chainId, DateTime sinceUtc, CancellationToken ct = default)
        {
            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            return cache.Tokens.Values
                .Where(t => t.AddedAtUtc > sinceUtc)
                .OrderBy(t => t.AddedAtUtc)
                .ToList();
        }

        public async Task<CatalogTokenInfo> GetTokenByAddressAsync(long chainId, string contractAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            var normalizedAddress = contractAddress.ToLowerInvariant();

            cache.Tokens.TryGetValue(normalizedAddress, out var token);
            return token;
        }

        public async Task<int> GetTokenCountAsync(long chainId, CancellationToken ct = default)
        {
            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            return cache.Tokens.Count;
        }

        public async Task<int> AddOrUpdateTokensAsync(long chainId, IEnumerable<CatalogTokenInfo> tokens, bool updateExisting = false, CancellationToken ct = default)
        {
            if (tokens == null)
                return 0;

            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            int addedCount = 0;
            int updatedCount = 0;

            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token.Address))
                    continue;

                var normalizedAddress = token.Address.ToLowerInvariant();
                token.ChainId = chainId;

                if (cache.Tokens.TryGetValue(normalizedAddress, out var existing))
                {
                    if (updateExisting)
                    {
                        existing.UpdateFrom(token);
                        updatedCount++;
                    }
                }
                else
                {
                    if (token.AddedAtUtc == default)
                        token.AddedAtUtc = DateTime.UtcNow;

                    cache.Tokens[normalizedAddress] = token;
                    addedCount++;
                }
            }

            if (addedCount > 0 || updatedCount > 0)
            {
                await SaveCacheAsync(chainId, cache, ct).ConfigureAwait(false);
            }

            return addedCount;
        }

        public async Task<bool> RemoveTokenAsync(long chainId, string contractAddress, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return false;

            var cache = await GetOrLoadCacheAsync(chainId, ct).ConfigureAwait(false);
            var normalizedAddress = contractAddress.ToLowerInvariant();

            if (cache.Tokens.Remove(normalizedAddress))
            {
                await SaveCacheAsync(chainId, cache, ct).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<bool> IsInitializedAsync(long chainId, CancellationToken ct = default)
        {
            var metadata = await GetMetadataAsync(chainId, ct).ConfigureAwait(false);
            return metadata?.IsSeeded == true;
        }

        public async Task SeedFromEmbeddedAsync(long chainId, bool forceReseed = false, CancellationToken ct = default)
        {
            await _seedLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!forceReseed)
                {
                    var isInitialized = await IsInitializedAsync(chainId, ct).ConfigureAwait(false);
                    if (isInitialized)
                        return;
                }

                var embeddedTokens = await _embeddedProvider.GetTokensAsync(chainId).ConfigureAwait(false);
                if (embeddedTokens == null || embeddedTokens.Count == 0)
                    return;

                var cache = new CatalogCache();
                var now = DateTime.UtcNow;

                foreach (var token in embeddedTokens)
                {
                    var catalogToken = CatalogTokenInfo.FromTokenInfo(token, "embedded");
                    catalogToken.AddedAtUtc = now;
                    var normalizedAddress = token.Address.ToLowerInvariant();
                    cache.Tokens[normalizedAddress] = catalogToken;
                }

                _chainCaches[chainId] = cache;
                await SaveCacheAsync(chainId, cache, ct).ConfigureAwait(false);

                var metadata = new CatalogMetadata
                {
                    IsSeeded = true,
                    SeededAtUtc = now,
                    TokenCount = cache.Tokens.Count
                };
                await SetMetadataAsync(chainId, metadata, ct).ConfigureAwait(false);
            }
            finally
            {
                _seedLock.Release();
            }
        }

        public async Task<CatalogMetadata> GetMetadataAsync(long chainId, CancellationToken ct = default)
        {
            var path = GetChainPath(chainId, MetadataFileName);
            return await ReadAsync<CatalogMetadata>(path, ct).ConfigureAwait(false) ?? new CatalogMetadata();
        }

        public async Task SetMetadataAsync(long chainId, CatalogMetadata metadata, CancellationToken ct = default)
        {
            var path = GetChainPath(chainId, MetadataFileName);
            await WriteAsync(path, metadata, ct).ConfigureAwait(false);
        }

        public async Task ClearAsync(long chainId, CancellationToken ct = default)
        {
            _chainCaches.TryRemove(chainId, out _);

            var catalogPath = GetChainPath(chainId, CatalogFileName);
            var metadataPath = GetChainPath(chainId, MetadataFileName);

            await DeleteAsync(catalogPath, ct).ConfigureAwait(false);
            await DeleteAsync(metadataPath, ct).ConfigureAwait(false);
        }

        private async Task<CatalogCache> GetOrLoadCacheAsync(long chainId, CancellationToken ct)
        {
            if (_chainCaches.TryGetValue(chainId, out var cache))
                return cache;

            var path = GetChainPath(chainId, CatalogFileName);
            var data = await ReadAsync<CatalogData>(path, ct).ConfigureAwait(false);

            cache = new CatalogCache();
            if (data?.Tokens != null)
            {
                foreach (var token in data.Tokens)
                {
                    if (!string.IsNullOrEmpty(token.Address))
                    {
                        var normalizedAddress = token.Address.ToLowerInvariant();
                        cache.Tokens[normalizedAddress] = token;
                    }
                }
            }

            _chainCaches[chainId] = cache;
            return cache;
        }

        private async Task SaveCacheAsync(long chainId, CatalogCache cache, CancellationToken ct)
        {
            var path = GetChainPath(chainId, CatalogFileName);
            var data = new CatalogData
            {
                ChainId = chainId,
                Tokens = cache.Tokens.Values.ToList(),
                LastModifiedUtc = DateTime.UtcNow
            };
            await WriteAsync(path, data, ct).ConfigureAwait(false);
        }

        private static string GetChainPath(long chainId, string fileName)
        {
            return Path.Combine(chainId.ToString(), fileName);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _seedLock.Dispose();
                _chainCaches.Clear();
            }
            base.Dispose(disposing);
        }

        private class CatalogCache
        {
            public Dictionary<string, CatalogTokenInfo> Tokens { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private class CatalogData
        {
            public long ChainId { get; set; }
            public List<CatalogTokenInfo> Tokens { get; set; } = new();
            public DateTime LastModifiedUtc { get; set; }
        }
    }
}
