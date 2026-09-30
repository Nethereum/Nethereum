using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class PayloadAttributesV4 : PayloadAttributesV3
    {
        [JsonProperty(PropertyName = "slotNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("slotNumber")]
#endif
        public HexBigInteger SlotNumber { get; set; }

        [JsonProperty(PropertyName = "targetGasLimit")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("targetGasLimit")]
#endif
        public HexBigInteger TargetGasLimit { get; set; }
    }
}
