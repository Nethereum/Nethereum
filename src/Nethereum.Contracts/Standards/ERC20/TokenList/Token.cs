#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json; 
#endif
using System;
using System.Collections.Generic;

namespace Nethereum.Contracts.Standards.ERC20.TokenList
{
    public class Token
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("chainId")]
#else
        [JsonProperty("chainId")]
#endif
        public int ChainId { get; set; }

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
}