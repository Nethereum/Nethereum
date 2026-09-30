using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.TxPool.DTOs
{
    public class TxPoolStatusResponse
    {
        [JsonProperty(PropertyName = "pending", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("pending")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Pending { get; set; }

        [JsonProperty(PropertyName = "queued", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("queued")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Queued { get; set; }
    }
}
