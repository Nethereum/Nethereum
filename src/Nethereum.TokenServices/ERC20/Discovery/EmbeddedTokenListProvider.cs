using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class EmbeddedTokenListProvider : ITokenListProvider
    {
        private static readonly long[] SupportedChainIds = { 1, 10, 56, 100, 137, 324, 8453, 42161, 42220, 43114, 59144 };
        private readonly Dictionary<long, List<TokenInfo>> _cache = new Dictionary<long, List<TokenInfo>>();
        private readonly object _lock = new object();

        public Task<List<TokenInfo>> GetTokensAsync(long chainId)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(chainId, out var cached))
                {
                    return Task.FromResult(cached);
                }
            }

            var tokens = LoadFromResource(chainId);

            if (tokens.Count > 0)
            {
                lock (_lock)
                {
                    _cache[chainId] = tokens;
                }
            }

            return Task.FromResult(tokens);
        }

        public async Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
            {
                return null;
            }

            var tokens = await GetTokensAsync(chainId);
            var token = tokens.FirstOrDefault(t =>
                string.Equals(t.Address, contractAddress, StringComparison.OrdinalIgnoreCase));

            return token;
        }

        public Task<bool> SupportsChainAsync(long chainId)
        {
            return Task.FromResult(Array.IndexOf(SupportedChainIds, chainId) >= 0);
        }

        private List<TokenInfo> LoadFromResource(long chainId)
        {
            var assembly = typeof(EmbeddedTokenListProvider).Assembly;
            var resourceName = $"Nethereum.TokenServices.Resources.tokenlist_{chainId}.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return new List<TokenInfo>();
                }

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var tokens = JsonSerializer.Deserialize<List<TokenInfo>>(json, options);
                    return tokens ?? new List<TokenInfo>();
                }
            }
        }

        public static IEnumerable<long> GetSupportedChainIds()
        {
            return SupportedChainIds;
        }
    }
}
