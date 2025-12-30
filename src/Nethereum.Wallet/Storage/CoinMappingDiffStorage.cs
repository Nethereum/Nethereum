using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Pricing;

namespace Nethereum.Wallet.Storage
{
    public class WalletCoinMappingDiffStorage : FileStorageBase, ICoinMappingDiffStorage
    {
        private const string MetadataFileName = "metadata.json";
        private CoinMappingMetadata _cachedMetadata;
        private readonly SemaphoreSlim _metadataLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _chainLocks = new ConcurrentDictionary<long, SemaphoreSlim>();

        private SemaphoreSlim GetChainLock(long chainId)
        {
            return _chainLocks.GetOrAdd(chainId, _ => new SemaphoreSlim(1, 1));
        }

        public WalletCoinMappingDiffStorage(
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
                "Nethereum", "Wallet", "tokens", "mappings");
        }

        public async Task<Dictionary<string, string>> GetAdditionalMappingsAsync(long chainId)
        {
            var path = GetChainFilePath(chainId);
            return await ReadAsync<Dictionary<string, string>>(path).ConfigureAwait(false)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task SaveAdditionalMappingsAsync(long chainId, Dictionary<string, string> mappings)
        {
            var path = GetChainFilePath(chainId);
            await WriteAsync(path, mappings ?? new Dictionary<string, string>()).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, string>> GetAndUpdateMappingsAsync(
            long chainId,
            Func<Dictionary<string, string>, Dictionary<string, string>> updateFunc)
        {
            var chainLock = GetChainLock(chainId);
            await chainLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = GetChainFilePath(chainId);
                var mappings = await ReadAsync<Dictionary<string, string>>(path).ConfigureAwait(false)
                               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var updated = updateFunc(mappings);
                await WriteAsync(path, updated).ConfigureAwait(false);
                return updated;
            }
            finally
            {
                chainLock.Release();
            }
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

        private async Task<CoinMappingMetadata> GetMetadataAsync()
        {
            if (_cachedMetadata != null)
                return _cachedMetadata;

            _cachedMetadata = await ReadAsync<CoinMappingMetadata>(MetadataFileName).ConfigureAwait(false)
                              ?? new CoinMappingMetadata();
            return _cachedMetadata;
        }

        private static string GetChainFilePath(long chainId) => $"{chainId}.json";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _metadataLock.Dispose();
                foreach (var chainLock in _chainLocks.Values)
                {
                    chainLock.Dispose();
                }
                _chainLocks.Clear();
            }
            base.Dispose(disposing);
        }
    }

    public class CoinMappingMetadata
    {
        public Dictionary<long, DateTime> LastUpdates { get; set; } = new Dictionary<long, DateTime>();
    }
}
