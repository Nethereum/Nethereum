using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ForkchoiceUpdatedResponseV1
    {
        [JsonProperty(PropertyName = "payloadStatus")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("payloadStatus")]
#endif
        public PayloadStatusV1 PayloadStatus { get; set; }

        [JsonProperty(PropertyName = "payloadId", NullValueHandling = NullValueHandling.Include)]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("payloadId")]
#endif
        public string PayloadId { get; set; }
    }
}
