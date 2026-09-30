using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ForkchoiceStateV1
    {
        [JsonProperty(PropertyName = "headBlockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("headBlockHash")]
#endif
        public string HeadBlockHash { get; set; }

        [JsonProperty(PropertyName = "safeBlockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("safeBlockHash")]
#endif
        public string SafeBlockHash { get; set; }

        [JsonProperty(PropertyName = "finalizedBlockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("finalizedBlockHash")]
#endif
        public string FinalizedBlockHash { get; set; }
    }
}
