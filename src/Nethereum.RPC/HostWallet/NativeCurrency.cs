using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class NativeCurrency
    {
        [JsonProperty(PropertyName = "name")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("name")]
#endif
        public string Name { get; set; }

        [JsonProperty(PropertyName = "symbol")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("symbol")]
#endif
        public string Symbol { get; set; }

        [JsonProperty(PropertyName = "decimals")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("decimals")]
#endif
        public uint Decimals { get; set; }
    }
}