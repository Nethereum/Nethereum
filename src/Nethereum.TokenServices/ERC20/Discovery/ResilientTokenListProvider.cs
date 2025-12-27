using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class ResilientTokenListProvider : ITokenListProvider
    {
        private readonly ITokenListProvider _remoteProvider;
        private readonly ITokenListProvider _embeddedProvider;
        private readonly ITokenListDiffStorage _diffStorage;
        private readonly ICacheProvider _cacheProvider;
        private readonly TimeSpan _cacheExpiry;
        private readonly ConcurrentDictionary<long, bool> _updateQueued = new ConcurrentDictionary<long, bool>();

        public ResilientTokenListProvider(
            ITokenListProvider remoteProvider,
            ITokenListProvider embeddedProvider,
            ITokenListDiffStorage diffStorage = null,
            ICacheProvider cacheProvider = null,
            TimeSpan? cacheExpiry = null)
        {
            _remoteProvider = remoteProvider ?? throw new ArgumentNullException(nameof(remoteProvider));
            _embeddedProvider = embeddedProvider ?? throw new ArgumentNullException(nameof(embeddedProvider));
            _diffStorage = diffStorage;
            _cacheProvider = cacheProvider ?? new MemoryCacheProvider();
            _cacheExpiry = cacheExpiry ?? TimeSpan.FromDays(7);
        }

        public async Task<List<TokenInfo>> GetTokensAsync(long chainId)
        {
            var cacheKey = $"tokenlist:{chainId}";

            var cached = await _cacheProvider.GetAsync<List<TokenInfo>>(cacheKey);
            if (cached != null && cached.Count > 0)
            {
                return cached;
            }

            var embedded = await _embeddedProvider.GetTokensAsync(chainId);

            List<TokenInfo> additional = null;
            if (_diffStorage != null)
            {
                additional = await _diffStorage.GetAdditionalTokensAsync(chainId);
            }

            if ((embedded != null && embedded.Count > 0) || (additional != null && additional.Count > 0))
            {
                var merged = MergeTokenLists(embedded, additional);
                QueueBackgroundUpdate(chainId);
                return merged;
            }

            try
            {
                var tokens = await _remoteProvider.GetTokensAsync(chainId);
                if (tokens != null && tokens.Count > 0)
                {
                    var filtered = BridgeTokenFilter.FilterBridgeTokens(tokens);
                    await _cacheProvider.SetAsync(cacheKey, filtered, _cacheExpiry);
                    return filtered;
                }
            }
            catch
            {
            }

            return new List<TokenInfo>();
        }

        private List<TokenInfo> MergeTokenLists(List<TokenInfo> embedded, List<TokenInfo> additional)
        {
            var result = new Dictionary<string, TokenInfo>(StringComparer.OrdinalIgnoreCase);

            if (embedded != null)
            {
                foreach (var token in embedded)
                {
                    if (!string.IsNullOrEmpty(token.Address))
                    {
                        result[token.Address] = token;
                    }
                }
            }

            if (additional != null)
            {
                foreach (var token in additional)
                {
                    if (!string.IsNullOrEmpty(token.Address) && !result.ContainsKey(token.Address))
                    {
                        result[token.Address] = token;
                    }
                }
            }

            return result.Values.ToList();
        }

        private void QueueBackgroundUpdate(long chainId)
        {
            if (!_updateQueued.TryAdd(chainId, true))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var cacheKey = $"tokenlist:{chainId}";
                    var tokens = await _remoteProvider.GetTokensAsync(chainId);
                    if (tokens != null && tokens.Count > 0)
                    {
                        var filtered = BridgeTokenFilter.FilterBridgeTokens(tokens);

                        if (_diffStorage != null)
                        {
                            var embedded = await _embeddedProvider.GetTokensAsync(chainId);
                            var embeddedAddresses = new HashSet<string>(
                                (embedded ?? new List<TokenInfo>()).Select(t => t.Address?.ToLowerInvariant() ?? ""),
                                StringComparer.OrdinalIgnoreCase);

                            var existingAdditional = await _diffStorage.GetAdditionalTokensAsync(chainId);
                            var existingAddresses = new HashSet<string>(
                                existingAdditional.Select(t => t.Address?.ToLowerInvariant() ?? ""),
                                StringComparer.OrdinalIgnoreCase);

                            var newTokens = filtered
                                .Where(t => !string.IsNullOrEmpty(t.Address) &&
                                            !embeddedAddresses.Contains(t.Address.ToLowerInvariant()) &&
                                            !existingAddresses.Contains(t.Address.ToLowerInvariant()))
                                .ToList();

                            if (newTokens.Any())
                            {
                                existingAdditional.AddRange(newTokens);
                                await _diffStorage.SaveAdditionalTokensAsync(chainId, existingAdditional);
                            }

                            await _diffStorage.SetLastUpdateAsync(chainId, DateTime.UtcNow);
                        }

                        await _cacheProvider.SetAsync(cacheKey, filtered, _cacheExpiry);
                    }
                }
                catch
                {
                }
            });
        }

        public async Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
            {
                return null;
            }

            var tokens = await GetTokensAsync(chainId);
            foreach (var token in tokens)
            {
                if (string.Equals(token.Address, contractAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return token;
                }
            }

            return null;
        }

        public async Task<bool> SupportsChainAsync(long chainId)
        {
            if (await _embeddedProvider.SupportsChainAsync(chainId))
            {
                return true;
            }

            return await _remoteProvider.SupportsChainAsync(chainId);
        }
    }
}
