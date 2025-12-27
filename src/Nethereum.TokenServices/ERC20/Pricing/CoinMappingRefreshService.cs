using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public class MappingRefreshResult
    {
        public bool Success { get; private set; }
        public bool Skipped { get; private set; }
        public int NewMappingsCount { get; private set; }
        public string ErrorMessage { get; private set; }

        public static MappingRefreshResult Succeeded(int newMappings) => new MappingRefreshResult { Success = true, NewMappingsCount = newMappings };
        public static MappingRefreshResult SkippedResult() => new MappingRefreshResult { Skipped = true };
        public static MappingRefreshResult NothingToUpdate() => new MappingRefreshResult { Success = true, NewMappingsCount = 0 };
        public static MappingRefreshResult Failed(string error) => new MappingRefreshResult { ErrorMessage = error };
    }

    public class CoinMappingRefreshService
    {
        private readonly CoinGeckoApiService _coinGeckoApi;
        private readonly EmbeddedCoinMappingProvider _embeddedProvider;
        private readonly ICoinMappingDiffStorage _diffStorage;
        private readonly TimeSpan _updateInterval;
        private readonly int _batchSize;
        private readonly TimeSpan _rateLimitDelay;

        public CoinMappingRefreshService(
            CoinGeckoApiService coinGeckoApi,
            EmbeddedCoinMappingProvider embeddedProvider,
            ICoinMappingDiffStorage diffStorage,
            TimeSpan? updateInterval = null,
            int batchSize = 10,
            TimeSpan? rateLimitDelay = null)
        {
            _coinGeckoApi = coinGeckoApi ?? throw new ArgumentNullException(nameof(coinGeckoApi));
            _embeddedProvider = embeddedProvider ?? throw new ArgumentNullException(nameof(embeddedProvider));
            _diffStorage = diffStorage ?? throw new ArgumentNullException(nameof(diffStorage));
            _updateInterval = updateInterval ?? TimeSpan.FromDays(1);
            _batchSize = batchSize;
            _rateLimitDelay = rateLimitDelay ?? TimeSpan.FromSeconds(1);
        }

        public async Task<MappingRefreshResult> RefreshMappingsAsync(
            long chainId,
            IEnumerable<string> contractAddresses = null,
            bool forceRefresh = false)
        {
            var lastUpdate = await _diffStorage.GetLastUpdateAsync(chainId);
            if (!forceRefresh && lastUpdate.HasValue && DateTime.UtcNow - lastUpdate.Value < _updateInterval)
            {
                return MappingRefreshResult.SkippedResult();
            }

            try
            {
                var embeddedMappings = _embeddedProvider.GetCoinMappings(chainId);
                var additionalMappings = await _diffStorage.GetAdditionalMappingsAsync(chainId);

                var allMapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in embeddedMappings.Keys)
                    allMapped.Add(key.ToLowerInvariant());
                foreach (var key in additionalMappings.Keys)
                    allMapped.Add(key.ToLowerInvariant());

                List<string> addressesToLookup;
                if (contractAddresses != null)
                {
                    addressesToLookup = contractAddresses
                        .Where(a => !string.IsNullOrEmpty(a) && !allMapped.Contains(a.ToLowerInvariant()))
                        .Select(a => a.ToLowerInvariant())
                        .Distinct()
                        .ToList();
                }
                else
                {
                    return MappingRefreshResult.NothingToUpdate();
                }

                if (!addressesToLookup.Any())
                {
                    await _diffStorage.SetLastUpdateAsync(chainId, DateTime.UtcNow);
                    return MappingRefreshResult.NothingToUpdate();
                }

                var newMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var batches = addressesToLookup
                    .Select((addr, index) => new { addr, index })
                    .GroupBy(x => x.index / _batchSize)
                    .Select(g => g.Select(x => x.addr).ToList())
                    .ToList();

                for (int i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var result = await _coinGeckoApi.FindCoinGeckoIdsAsync(batch, chainId);
                    foreach (var kvp in result)
                    {
                        newMappings[kvp.Key] = kvp.Value;
                    }

                    if (i < batches.Count - 1)
                    {
                        await Task.Delay(_rateLimitDelay);
                    }
                }

                if (newMappings.Any())
                {
                    await _diffStorage.GetAndUpdateMappingsAsync(chainId, existing =>
                    {
                        foreach (var kvp in newMappings)
                        {
                            existing[kvp.Key] = kvp.Value;
                        }
                        return existing;
                    });
                }

                await _diffStorage.SetLastUpdateAsync(chainId, DateTime.UtcNow);

                return MappingRefreshResult.Succeeded(newMappings.Count);
            }
            catch (Exception ex)
            {
                return MappingRefreshResult.Failed(ex.Message);
            }
        }

        public async Task<Dictionary<string, string>> GetMergedMappingsAsync(long chainId)
        {
            var embedded = _embeddedProvider.GetCoinMappings(chainId);
            var additional = await _diffStorage.GetAdditionalMappingsAsync(chainId);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in embedded)
            {
                result[kvp.Key.ToLowerInvariant()] = kvp.Value;
            }

            foreach (var kvp in additional)
            {
                var key = kvp.Key.ToLowerInvariant();
                if (!result.ContainsKey(key))
                {
                    result[key] = kvp.Value;
                }
            }

            return result;
        }
    }
}
