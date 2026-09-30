using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ExecutionPayloadV4 : ExecutionPayloadV3
    {
        [JsonProperty(PropertyName = "blockAccessList")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockAccessList")]
#endif
        public string BlockAccessList { get; set; }

        [JsonProperty(PropertyName = "slotNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("slotNumber")]
#endif
        public HexBigInteger SlotNumber { get; set; }
    }
}
