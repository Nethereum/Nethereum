using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.CoinGecko.Responses
{
    public class CoinGeckoTokenList
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("logoURI")]
#else
        [JsonProperty("logoURI")]
#endif
        public string LogoURI { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("timestamp")]
#else
        [JsonProperty("timestamp")]
#endif
        public DateTime Timestamp { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("tokens")]
#else
        [JsonProperty("tokens")]
#endif
        public List<CoinGeckoToken> Tokens { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public CoinGeckoTokenListVersion Version { get; set; }
    }

    public class CoinGeckoToken
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("chainId")]
#else
        [JsonProperty("chainId")]
#endif
        public long ChainId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("symbol")]
#else
        [JsonProperty("symbol")]
#endif
        public string Symbol { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("decimals")]
#else
        [JsonProperty("decimals")]
#endif
        public int Decimals { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("logoURI")]
#else
        [JsonProperty("logoURI")]
#endif
        public string LogoURI { get; set; }
    }

    public class CoinGeckoTokenListVersion
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("major")]
#else
        [JsonProperty("major")]
#endif
        public int Major { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("minor")]
#else
        [JsonProperty("minor")]
#endif
        public int Minor { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("patch")]
#else
        [JsonProperty("patch")]
#endif
        public int Patch { get; set; }
    }
}
