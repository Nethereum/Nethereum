using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.Wallet.Storage
{
    public class WalletTokenListDiffStorage : FileStorageBase, ITokenListDiffStorage
    {
        private const string MetadataFileName = "metadata.json";
        private TokenListMetadata _cachedMetadata;
        private readonly SemaphoreSlim _metadataLock = new SemaphoreSlim(1, 1);

        public WalletTokenListDiffStorage(
            string baseDirectory = null,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
            : base(
                baseDirectory ?? GetDefaultDirectory(),
                jsonOptions,
                onError)
        {
        }

        private static string GetDefaultDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "Wallet", "tokens", "lists");
        }

        public async Task<List<TokenInfo>> GetAdditionalTokensAsync(long chainId)
        {
            var path = GetChainFilePath(chainId);
            return await ReadAsync<List<TokenInfo>>(path).ConfigureAwait(false) ?? new List<TokenInfo>();
        }

        public async Task SaveAdditionalTokensAsync(long chainId, List<TokenInfo> tokens)
        {
            var path = GetChainFilePath(chainId);
            await WriteAsync(path, tokens ?? new List<TokenInfo>()).ConfigureAwait(false);
        }

        public async Task<DateTime?> GetLastUpdateAsync(long chainId)
        {
            var metadata = await GetMetadataAsync().ConfigureAwait(false);
            if (metadata.LastUpdates.TryGetValue(chainId, out var updateTime))
                return updateTime;
            return null;
        }

        public async Task SetLastUpdateAsync(long chainId, DateTime updateTime)
        {
            await _metadataLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var metadata = await GetMetadataAsync().ConfigureAwait(false);
                metadata.LastUpdates[chainId] = updateTime;
                await WriteAsync(MetadataFileName, metadata).ConfigureAwait(false);
                _cachedMetadata = metadata;
            }
            finally
            {
                _metadataLock.Release();
            }
        }

        public async Task ClearAsync(long chainId)
        {
            var path = GetChainFilePath(chainId);
            await DeleteAsync(path).ConfigureAwait(false);

            await _metadataLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var metadata = await GetMetadataAsync().ConfigureAwait(false);
                metadata.LastUpdates.Remove(chainId);
                await WriteAsync(MetadataFileName, metadata).ConfigureAwait(false);
                _cachedMetadata = metadata;
            }
            finally
            {
                _metadataLock.Release();
            }
        }

        private async Task<TokenListMetadata> GetMetadataAsync()
        {
            if (_cachedMetadata != null)
                return _cachedMetadata;

            _cachedMetadata = await ReadAsync<TokenListMetadata>(MetadataFileName).ConfigureAwait(false)
                              ?? new TokenListMetadata();
            return _cachedMetadata;
        }

        private static string GetChainFilePath(long chainId) => $"{chainId}.json";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _metadataLock.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TokenListMetadata
    {
        public Dictionary<long, DateTime> LastUpdates { get; set; } = new Dictionary<long, DateTime>();
    }
}
