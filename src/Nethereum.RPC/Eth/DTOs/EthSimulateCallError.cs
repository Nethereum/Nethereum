using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class EthSimulateCallError
    {
        [JsonProperty(PropertyName = "message")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("message")]
#endif
        public string Message { get; set; }

        [JsonProperty(PropertyName = "code")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("code")]
#endif
        public int Code { get; set; }

        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("data")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string Data { get; set; }
    }
}
