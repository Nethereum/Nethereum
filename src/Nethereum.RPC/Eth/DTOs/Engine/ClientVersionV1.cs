using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ClientVersionV1
    {
        [JsonProperty(PropertyName = "code")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("code")]
#endif
        public string Code { get; set; }

        [JsonProperty(PropertyName = "name")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("name")]
#endif
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("version")]
#endif
        public string Version { get; set; }

        [JsonProperty(PropertyName = "commit")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("commit")]
#endif
        public string Commit { get; set; }
    }
}
