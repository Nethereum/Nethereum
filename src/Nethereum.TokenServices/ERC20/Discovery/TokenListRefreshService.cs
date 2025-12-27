using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class TokenListRefreshResult
    {
        public bool Success { get; private set; }
        public bool Skipped { get; private set; }
        public int NewTokensCount { get; private set; }
        public string ErrorMessage { get; private set; }

        public static TokenListRefreshResult Succeeded(int newTokens) => new TokenListRefreshResult { Success = true, NewTokensCount = newTokens };
        public static TokenListRefreshResult SkippedResult() => new TokenListRefreshResult { Skipped = true };
        public static TokenListRefreshResult Failed(string error) => new TokenListRefreshResult { ErrorMessage = error };
    }

    public class TokenListRefreshService
    {
        private readonly ITokenListProvider _remoteProvider;
        private readonly EmbeddedTokenListProvider _embeddedProvider;
        private readonly ITokenListDiffStorage _diffStorage;
        private readonly TimeSpan _updateInterval;

        public TokenListRefreshService(
            ITokenListProvider remoteProvider,
            EmbeddedTokenListProvider embeddedProvider,
            ITokenListDiffStorage diffStorage,
            TimeSpan? updateInterval = null)
        {
            _remoteProvider = remoteProvider ?? throw new ArgumentNullException(nameof(remoteProvider));
            _embeddedProvider = embeddedProvider ?? throw new ArgumentNullException(nameof(embeddedProvider));
            _diffStorage = diffStorage ?? throw new ArgumentNullException(nameof(diffStorage));
            _updateInterval = updateInterval ?? TimeSpan.FromDays(1);
        }

        public async Task<TokenListRefreshResult> RefreshTokenListAsync(long chainId, bool forceRefresh = false)
        {
            var lastUpdate = await _diffStorage.GetLastUpdateAsync(chainId);
            if (!forceRefresh && lastUpdate.HasValue && DateTime.UtcNow - lastUpdate.Value < _updateInterval)
            {
                return TokenListRefreshResult.SkippedResult();
            }

            try
            {
                var embeddedTokens = await _embeddedProvider.GetTokensAsync(chainId) ?? new List<TokenInfo>();
                var embeddedAddresses = new HashSet<string>(
                    embeddedTokens.Select(t => t.Address?.ToLowerInvariant() ?? ""),
                    StringComparer.OrdinalIgnoreCase);

                var remoteTokens = await _remoteProvider.GetTokensAsync(chainId);
                if (remoteTokens == null || !remoteTokens.Any())
                {
                    return TokenListRefreshResult.Failed("Could not fetch remote tokens");
                }

                remoteTokens = BridgeTokenFilter.FilterBridgeTokens(remoteTokens);

                var existingAdditional = await _diffStorage.GetAdditionalTokensAsync(chainId);
                var existingAdditionalAddresses = new HashSet<string>(
                    existingAdditional.Select(t => t.Address?.ToLowerInvariant() ?? ""),
                    StringComparer.OrdinalIgnoreCase);

                var newTokens = remoteTokens
                    .Where(t => !string.IsNullOrEmpty(t.Address) &&
                                !embeddedAddresses.Contains(t.Address.ToLowerInvariant()) &&
                                !existingAdditionalAddresses.Contains(t.Address.ToLowerInvariant()))
                    .ToList();

                if (newTokens.Any())
                {
                    existingAdditional.AddRange(newTokens);
                    await _diffStorage.SaveAdditionalTokensAsync(chainId, existingAdditional);
                }

                await _diffStorage.SetLastUpdateAsync(chainId, DateTime.UtcNow);

                return TokenListRefreshResult.Succeeded(newTokens.Count);
            }
            catch (Exception ex)
            {
                return TokenListRefreshResult.Failed(ex.Message);
            }
        }

        public async Task<List<TokenInfo>> GetMergedTokenListAsync(long chainId)
        {
            var embedded = await _embeddedProvider.GetTokensAsync(chainId) ?? new List<TokenInfo>();
            var additional = await _diffStorage.GetAdditionalTokensAsync(chainId);

            var result = new Dictionary<string, TokenInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in embedded)
            {
                if (!string.IsNullOrEmpty(token.Address))
                {
                    result[token.Address] = token;
                }
            }

            foreach (var token in additional)
            {
                if (!string.IsNullOrEmpty(token.Address) && !result.ContainsKey(token.Address))
                {
                    result[token.Address] = token;
                }
            }

            return result.Values.ToList();
        }
    }
}
