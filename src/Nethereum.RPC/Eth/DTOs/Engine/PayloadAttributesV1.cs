using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class PayloadAttributesV1
    {
        [JsonProperty(PropertyName = "timestamp")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
#endif
        public HexBigInteger Timestamp { get; set; }

        [JsonProperty(PropertyName = "prevRandao")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("prevRandao")]
#endif
        public string PrevRandao { get; set; }

        [JsonProperty(PropertyName = "suggestedFeeRecipient")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("suggestedFeeRecipient")]
#endif
        public string SuggestedFeeRecipient { get; set; }
    }
}
