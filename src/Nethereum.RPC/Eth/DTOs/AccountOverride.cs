using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccountOverride
    {
        [JsonProperty(PropertyName = "balance", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("balance")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Balance { get; set; }

        [JsonProperty(PropertyName = "nonce", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonce")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "code", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("code")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string Code { get; set; }

        [JsonProperty(PropertyName = "movePrecompileToAddress", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("movePrecompileToAddress")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string MovePrecompileToAddress { get; set; }

        [JsonProperty(PropertyName = "state", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("state")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Dictionary<string, string> State { get; set; }

        [JsonProperty(PropertyName = "stateDiff", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("stateDiff")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Dictionary<string, string> StateDiff { get; set; }
    }
}
