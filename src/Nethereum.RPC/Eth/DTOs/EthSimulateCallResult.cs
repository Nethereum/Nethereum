using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class EthSimulateCallResult
    {
        [JsonProperty(PropertyName = "status")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("status")]
#endif
        public string Status { get; set; }

        [JsonProperty(PropertyName = "returnData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("returnData")]
#endif
        public string ReturnData { get; set; }

        [JsonProperty(PropertyName = "gasUsed")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasUsed")]
#endif
        public HexBigInteger GasUsed { get; set; }

        [JsonProperty(PropertyName = "maxUsedGas", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("maxUsedGas")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger MaxUsedGas { get; set; }

        [JsonProperty(PropertyName = "logs", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("logs")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<FilterLog> Logs { get; set; }

        [JsonProperty(PropertyName = "error", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("error")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public object Error { get; set; }
    }
}
