#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json; 
#endif
using System;
using System.Collections.Generic;

namespace Nethereum.Contracts.Standards.ERC20.TokenList
{
    public class Version
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
