#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json;
#endif
using System;
using System.Collections.Generic;


namespace Nethereum.Contracts.Standards.ERC20.TokenList
{
    public class Root
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("timestamp")]
#else
        [JsonProperty("timestamp")]
#endif
        public DateTime Timestamp { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("keywords")]
#else
        [JsonProperty("keywords")]
#endif
        public List<string> Keywords { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public Version Version { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("tokens")]
#else
        [JsonProperty("tokens")]
#endif
        public List<Token> Tokens { get; set; }
    }
}