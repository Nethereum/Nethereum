using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public class EmbeddedCoinMappingProvider
    {
        private static readonly long[] SupportedChainIds = { 1, 10, 56, 100, 137, 324, 8453, 42161, 42220, 43114, 59144 };
        private readonly Dictionary<long, Dictionary<string, string>> _coinMappings = new Dictionary<long, Dictionary<string, string>>();
        private readonly Dictionary<long, PlatformInfo> _platforms;
        private readonly object _lock = new object();

        public EmbeddedCoinMappingProvider()
        {
            _platforms = LoadPlatforms();
        }

        public Dictionary<string, string> GetCoinMappings(long chainId)
        {
            lock (_lock)
            {
                if (_coinMappings.TryGetValue(chainId, out var cached))
                {
                    return cached;
                }
            }

            var mappings = LoadCoinMappingsFromResource(chainId);

            if (mappings.Count > 0)
            {
                lock (_lock)
                {
                    _coinMappings[chainId] = mappings;
                }
            }

            return mappings;
        }

        public string GetCoinId(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            var mappings = GetCoinMappings(chainId);

            if (mappings.TryGetValue(contractAddress.ToLowerInvariant(), out var coinId))
            {
                return coinId;
            }

            return null;
        }

        public string GetNativeCoinId(long chainId)
        {
            if (_platforms.TryGetValue(chainId, out var platform))
            {
                return platform.NativeCoinId;
            }
            return null;
        }

        public PlatformInfo GetPlatform(long chainId)
        {
            _platforms.TryGetValue(chainId, out var platform);
            return platform;
        }

        public bool SupportsChain(long chainId)
        {
            return Array.IndexOf(SupportedChainIds, chainId) >= 0;
        }

        public static IEnumerable<long> GetSupportedChainIds()
        {
            return SupportedChainIds;
        }

        private Dictionary<string, string> LoadCoinMappingsFromResource(long chainId)
        {
            var assembly = typeof(EmbeddedCoinMappingProvider).Assembly;
            var resourceName = $"Nethereum.TokenServices.Resources.coingecko.coins_{chainId}.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return new Dictionary<string, string>();
                }

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
                    return mappings ?? new Dictionary<string, string>();
                }
            }
        }

        private Dictionary<long, PlatformInfo> LoadPlatforms()
        {
            var assembly = typeof(EmbeddedCoinMappingProvider).Assembly;
            var resourceName = "Nethereum.TokenServices.Resources.coingecko.platforms.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return new Dictionary<long, PlatformInfo>();
                }

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var platforms = JsonSerializer.Deserialize<Dictionary<long, PlatformInfo>>(json, options);
                    return platforms ?? new Dictionary<long, PlatformInfo>();
                }
            }
        }
    }

    public class PlatformInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NativeCoinId { get; set; }
    }
}
