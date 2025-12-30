using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.CoinGecko.Responses
{
    public class CoinGeckoAssetPlatform
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("id")]
#else
        [JsonProperty("id")]
#endif
        public string Id { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chain_identifier")]
#else
        [JsonProperty("chain_identifier")]
#endif
        public long? ChainIdentifier { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("shortname")]
#else
        [JsonProperty("shortname")]
#endif
        public string ShortName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("native_coin_id")]
#else
        [JsonProperty("native_coin_id")]
#endif
        public string NativeCoinId { get; set; }
    }
}
