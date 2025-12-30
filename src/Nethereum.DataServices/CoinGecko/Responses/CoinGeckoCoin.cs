using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.CoinGecko.Responses
{
    public class CoinGeckoCoin
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("id")]
#else
        [JsonProperty("id")]
#endif
        public string Id { get; set; }

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
        [JsonPropertyName("platforms")]
#else
        [JsonProperty("platforms")]
#endif
        public Dictionary<string, string> Platforms { get; set; }
    }
}
